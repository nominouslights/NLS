# Dispatch Console (Admin Web App).
#
# Build context is THIS directory:
#   docker build -t northernlink-dispatcher Dispatcher/
# There is no npm workspace at the repo root — each app builds independently from its own
# package-lock.json, deliberately (see Budgeting/CLAUDE.md on why the design system is copied
# rather than shared).

# ---------- deps ----------
FROM node:22-alpine AS deps
RUN apk add --no-cache libc6-compat
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci

# ---------- build ----------
FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Where the server-side /api/* proxy points. Baked in here, not at runtime: rewrites are
# resolved during `next build` into .next/routes-manifest.json (see next.config.ts). The bare
# hostname resolves on the deployment's private network, so one image serves every environment
# as long as the API is reachable there as `northernlink-api` on 8080 — on DigitalOcean App
# Platform that means a service component of that exact name with internal_ports: [8080]
# (see .do/app.yaml), since a component's name is its hostname on the app's LAN.
ARG API_PROXY_TARGET=http://northernlink-api:8080
ENV API_PROXY_TARGET=$API_PROXY_TARGET

# Compiled into the client bundle (that is what NEXT_PUBLIC_ means), so it must be a build arg
# and will be visible in image layer history. Acceptable only because this key is public by
# design — keep it HTTP-referrer-restricted in Google Cloud and never reuse it server-side.
ARG NEXT_PUBLIC_GOOGLE_MAPS_API_KEY=""
ENV NEXT_PUBLIC_GOOGLE_MAPS_API_KEY=$NEXT_PUBLIC_GOOGLE_MAPS_API_KEY

# Path prefix this console is mounted at, when the three frontends share one hostname instead
# of getting one each. Both of these bake in at build time and must agree with each other and
# with the App Platform ingress rule (preserve_path_prefix: true) — see next.config.ts.
#   unset  → mounted at /, browser fetches /api/*        (local dev, hostname-per-app)
#   /dispatch → mounted at /dispatch, fetches /dispatch/api/*  (the DigitalOcean image)
# NEXT_PUBLIC_API_BASE_URL is otherwise left empty on purpose: empty means same-origin, which
# is the whole point of proxying /api/* through this server.
ARG BASE_PATH=""
ENV BASE_PATH=$BASE_PATH
ARG NEXT_PUBLIC_API_BASE_URL=""
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL

ENV NEXT_TELEMETRY_DISABLED=1
RUN npm run build

# public/ is empty here and absent entirely in Website/ and Budgeting/. Create it so the
# runtime COPY below is uniform across all three apps instead of failing on a missing path.
RUN mkdir -p public

# ---------- runtime ----------
FROM node:22-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1 \
    PORT=3000 \
    HOSTNAME=0.0.0.0

RUN addgroup --system --gid 1001 nodejs \
 && adduser --system --uid 1001 nextjs

# standalone carries server.js plus only the traced dependencies; static assets and public/
# are not traced and have to come across separately.
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nodejs /app/public ./public

# Writable even under a read-only root filesystem — the server writes its fetch/ISR cache here.
RUN mkdir -p .next/cache && chown -R nextjs:nodejs .next

USER nextjs
EXPOSE 3000

# Port 3000 inside the container for all three frontends; the 3001/3002/3003 split is a
# local-dev convention that the Service and Ingress replace.
CMD ["node", "server.js"]
