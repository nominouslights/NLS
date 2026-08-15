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
# resolved during `next build` into .next/routes-manifest.json (see next.config.ts). A bare
# Kubernetes Service name resolves within the pod's own namespace, so one image serves every
# environment as long as each namespace has a Service called northernlink-api.
ARG API_PROXY_TARGET=http://northernlink-api:8080
ENV API_PROXY_TARGET=$API_PROXY_TARGET

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
