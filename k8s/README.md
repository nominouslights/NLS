# Kubernetes manifests

Plain YAML plus Kustomize. Four workloads — the API and the three Next.js frontends — plus
RabbitMQ. `CommunityMobile/` is not deployed (Flutter design mockup), and `AppHost/` stays a
local-dev orchestrator that is never containerized.

```
k8s/
  base/                 every object, with production hostnames
  overlays/staging/     own namespace + staging hostnames
  overlays/production/  base hostnames, pinned image tags
```

## First apply

```sh
# 1. Secrets — never committed. secret.yaml is gitignored.
cp k8s/base/secret.example.yaml k8s/base/secret.yaml
$EDITOR k8s/base/secret.yaml
kubectl create namespace northern-link-staging
kubectl apply -n northern-link-staging -f k8s/base/secret.yaml

# 2. Everything else.
kubectl apply -k k8s/overlays/staging
```

Pin real image tags in the overlay's `images:` block before a production apply — `:latest` is a
rollback you cannot perform, because `kubectl rollout undo` would land on the same moving tag.

## Four things that are easy to get wrong

**The API has no Ingress rule, and must never get one.** The platform carries no CORS
configuration anywhere by design: a browser only ever talks to a frontend's own origin, and the
Next.js server proxies `/api/*` to the API in-cluster. Exposing `northernlink-api` publicly
would break that premise and force a CORS policy into the backend.

**The API runs exactly one replica.** Each pod hosts eight `OutboxDispatcher` background
services plus `TripGenerationWorker`, and `OutboxDispatcher`'s own doc comment records that
`FOR UPDATE SKIP LOCKED` is deliberately omitted. A second replica double-publishes integration
events and duplicates generated trips. The Deployment therefore uses `strategy: Recreate` too,
so a rolling surge never briefly runs two. Scaling out needs `SKIP LOCKED` or leader election
first.

**Frontend images are configured at build time, not here.** `next build` resolves rewrites into
`routes-manifest.json` and inlines every `NEXT_PUBLIC_*` value into the client bundle, so
`API_PROXY_TARGET` set on a running pod does nothing. The images bake
`http://northernlink-api:8080`; per-environment resolution comes from Kubernetes DNS, since a
bare Service name resolves inside the pod's own namespace. That is why the frontend Deployments
carry no `env:` block — anything there would look like configuration while having no effect.

**Readiness is the migration gate.** `Migrations__RunOnStartup=true` makes the API apply every
module's schema at boot under one Postgres advisory lock. Kestrel starts listening before any
hosted service runs, so `/health` failing until migrations complete is what actually keeps
traffic off a half-migrated schema — hence the `startupProbe` with a five-minute budget.
`/alive` stays a pure process check so a database outage drains pods instead of restart-looping
them.

## Cluster prerequisites

- **Postgres** is the managed DigitalOcean cluster, not something in this namespace. The
  cluster's egress addresses must be added to its **Trusted Sources** firewall, or every pod
  fails readiness on connect.
- The connection string must authenticate as **`northernlink_app`**, never a superuser — a
  superuser bypasses Row-Level Security even with `FORCE`, making every RLS policy dead code.
  That role also needs `GRANT CREATE ON DATABASE` for startup migrations; see
  `Backend/docker/initdb/01-app-role.sql`.
- **No PgBouncer in transaction pooling mode.** `TenantSessionInterceptor` sets `app.tenant_id`
  per connection, and transaction pooling would hand that session to another tenant's query.
- An ingress controller (`ingressClassName: nginx`) and cert-manager with a `letsencrypt-prod`
  ClusterIssuer, or edit `base/ingress.yaml` to match what the cluster actually runs.

## Images

Built and pushed by `.github/workflows/build.yml` to
`ghcr.io/<owner>/northernlink-{api,dispatcher,website,budgeting}`. If the packages are private,
the namespace needs an image pull secret referenced from each pod spec.
