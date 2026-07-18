-- Non-superuser application role — LIVE SERVER PROVISIONING SCRIPT.
--
-- WHY: a superuser bypasses Row-Level Security even with FORCE, so connecting the API as
-- one would make every RLS policy dead code. Run this once against the target Postgres
-- server (the live server, not local dev — see below) to create the role the API's
-- ConnectionStrings:Postgres should authenticate as.
--
-- Chosen arrangement (simplest that works): EF Core migrations run at API startup as
-- northernlink_app itself. GRANT CREATE on the database lets it create the module
-- schemas (fleet, ...); it then owns those schemas and their tables, and because every
-- migration issues FORCE ROW LEVEL SECURITY, RLS still binds the owner. No second
-- migration role or ownership juggling is needed.
--
-- NOT auto-run locally: local dev (via the Aspire AppHost or a standalone `dotnet run`)
-- connects to Postgres as the plain superuser — RLS is unenforced in local dev by design;
-- only the live server (where this script has been run) actually enforces it. Local RLS
-- testing, if ever needed again, means running this script by hand against a local Postgres.

CREATE ROLE northernlink_app LOGIN PASSWORD 'northernlink_dev';

GRANT CONNECT ON DATABASE northernlink TO northernlink_app;
GRANT CREATE ON DATABASE northernlink TO northernlink_app;

-- Second, LOGIN-less role that owns the read-side materialized views and their tenant
-- wrapper views. WHY a second role: RLS policies can't be attached to a materialized view,
-- so the tenant backstop is the wrapping plain view — but if the matview were owned by
-- northernlink_app we couldn't REVOKE the app's direct SELECT on it without also breaking
-- the wrapper (a view runs with its owner's privileges only when owned by a *different*
-- role). So the projector owns mv_* and v_*; the app is GRANTed SELECT on v_* only and has
-- no privilege on mv_* — a genuine table-privilege backstop paired with the EF query filter.
-- Membership (GRANT projector TO app) lets the app both `ALTER … OWNER TO projector` during
-- migrations and `SET ROLE projector` to REFRESH the matviews from the projection worker.
-- No request-path code ever runs as projector.
CREATE ROLE northernlink_projector NOLOGIN;
-- WITH INHERIT FALSE: the app can SET ROLE projector (to REFRESH) and reassign object
-- ownership to it during migrations, but does NOT automatically inherit projector's
-- privileges — so a raw SELECT on a projector-owned matview as the app role is denied by
-- table privilege, which is the whole point of the backstop.
GRANT northernlink_projector TO northernlink_app WITH INHERIT FALSE;
