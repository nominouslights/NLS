# Zero-Based Budgeting console.
#
# Build context is THIS directory:
#   docker build -t northernlink-budgeting Budgeting/
# Mirrors Dispatcher/Dockerfile — this app deliberately does not share a package with it.

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

# Path prefix this console is mounted at, when the three frontends share one hostname instead
# of getting one each. Both bake in at build time and must agree with each other and with the
# App Platform ingress rule (preserve_path_prefix: true) — see next.config.ts.
#   unset   → mounted at /, browser fetches /api/*          (local dev, hostname-per-app)
#   /budget → mounted at /budget, fetches /budget/api/*      (the DigitalOcean image)
ARG BASE_PATH=""
ENV BASE_PATH=$BASE_PATH
ARG NEXT_PUBLIC_API_BASE_URL=""
ENV NEXT_PUBLIC_API_BASE_URL=$NEXT_PUBLIC_API_BASE_URL

ENV NEXT_TELEMETRY_DISABLED=1
RUN npm run build

# This app has no public/ directory at all. Create it so the runtime COPY below is uniform
# across all three frontends instead of failing on a missing path.
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

COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nodejs /app/public ./public

RUN mkdir -p .next/cache && chown -R nextjs:nodejs .next

USER nextjs
EXPOSE 3000

CMD ["node", "server.js"]
