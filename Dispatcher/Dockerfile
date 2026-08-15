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
# resolved during `next build` into .next/routes-manifest.json (see next.config.ts). A bare
# Kubernetes Service name resolves within the pod's own namespace, so one image serves every
# environment as long as each namespace has a Service called northernlink-api.
ARG API_PROXY_TARGET=http://northernlink-api:8080
ENV API_PROXY_TARGET=$API_PROXY_TARGET

# Compiled into the client bundle (that is what NEXT_PUBLIC_ means), so it must be a build arg
# and will be visible in image layer history. Acceptable only because this key is public by
# design — keep it HTTP-referrer-restricted in Google Cloud and never reuse it server-side.
ARG NEXT_PUBLIC_GOOGLE_MAPS_API_KEY=""
ENV NEXT_PUBLIC_GOOGLE_MAPS_API_KEY=$NEXT_PUBLIC_GOOGLE_MAPS_API_KEY

# NEXT_PUBLIC_API_BASE_URL is deliberately left unset: empty means same-origin, which is the
# whole point of proxying /api/* through this server.

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
