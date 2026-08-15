# Public marketing site.
#
# Build context is THIS directory:
#   docker build -t northernlink-website Website/
# No build args: this app reads no environment variables and makes no API calls (its forms
# validate client-side and confirm in place). When real public endpoints exist it gains an
# /api/* rewrite mirroring Dispatcher's, and with it an API_PROXY_TARGET build arg.

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
