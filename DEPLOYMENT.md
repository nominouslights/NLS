# Deployment Guide

## Overview

The Shuttle platform consists of two components:

| Component | Technology | Deployed at |
|---|---|---|
| `shuttle-api` | .NET 10, PostgreSQL | DigitalOcean App Platform |
| `shuttle_tablet` | Flutter | Android/tablet devices |

All runtime configuration is supplied exclusively via environment variables. No secrets live in committed files.

---

## shuttle-api

### Environment variables

Every value the API needs is read from the environment. There are no fallback secrets in `appsettings.json`.

| Variable | Required | Default | Description |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | Yes | — | Full Npgsql connection string |
| `JwtSettings__Secret` | Yes | — | HMAC-256 signing key, 32+ characters |
| `JwtSettings__Issuer` | No | `shuttle-api` | JWT issuer claim |
| `JwtSettings__Audience` | No | `shuttle-tablet` | JWT audience claim |
| `JwtSettings__AccessTokenExpiryMinutes` | No | `15` | Access token lifetime in minutes |
| `JwtSettings__RefreshTokenExpiryDays` | No | `7` | Refresh token lifetime in days |

> The double-underscore (`__`) separator is how ASP.NET Core maps environment variables to nested configuration sections.

### Database migrations

Migrations run automatically on every startup via `db.Database.MigrateAsync()` before the app begins serving traffic. The initial admin user is seeded on first boot if no users exist.

Default seed credentials (change after first login):
- **Email:** `admin@northernlink.com`
- **Password:** `Admin123!`

To run migrations manually against any database:

```bash
dotnet ef database update \
  --project src/ShuttleApi.Infrastructure \
  --startup-project src/ShuttleApi.Api \
  --connection "<connection-string>"
```

---

## Local development

### Prerequisites

- Docker Desktop
- A `.env` file at `shuttle-api/.env` (gitignored)

### shuttle-api `.env`

```env
POSTGRES_PASSWORD=shuttle_pass_dev
JWT_SECRET=local_dev_secret_at_least_32_characters_long
```

Optional overrides (defaults shown):

```env
JWT_ISSUER=shuttle-api
JWT_AUDIENCE=shuttle-tablet
JWT_ACCESS_EXPIRY_MINUTES=15
JWT_REFRESH_EXPIRY_DAYS=7
```

### Start the stack

```bash
cd shuttle-api
docker compose up --build
```

The API will be available at `http://localhost:5046`. Migrations and seeding run on first boot.

### shuttle_tablet `.env`

Located at `shuttle_tablet/.env` (gitignored, declared as a Flutter asset):

```env
API_BASE_URL=http://10.0.2.2:5046/api   # Android emulator → host
# API_BASE_URL=http://192.168.x.x:5046/api  # Physical device → host IP
```

---

## DigitalOcean App Platform (production)

### API environment variables

Set the following as **Encrypted** variables in Apps → your app → Settings → App-Level Environment Variables:

| Variable | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | `Host=<db-host>;Port=25060;Database=defaultdb;Username=doadmin;Password=<password>;SSL Mode=Require;Trust Server Certificate=true` |
| `JwtSettings__Secret` | A securely generated 32+ character string |
| `JwtSettings__Issuer` | `shuttle-api` |
| `JwtSettings__Audience` | `shuttle-tablet` |

> `Trust Server Certificate=true` is required because DigitalOcean managed PostgreSQL uses a private CA not present in the default Docker container trust store.

### shuttle_tablet `.env` (pointing at production)

```env
API_BASE_URL=https://<your-app>.ondigitalocean.app/api
```

### Deploy

Push to `main`. The GitHub Actions workflow (`.github/workflows/build-and-publish.yml`) builds the Docker image, pushes it to GHCR, and DigitalOcean redeploys automatically.

---

## Health check

```
GET /health  →  200 "Healthy"
```

No authentication required. Use this to confirm a deployment is live before routing traffic.
