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

-- This is the ONLY role the platform needs. A second, LOGIN-less northernlink_projector role
-- used to live here, owning the read-side materialized views and their tenant wrapper views —
-- the workaround for Postgres not supporting RLS policies on a matview. The read side is now
-- ordinary projector-maintained tables (fleet.rm_*) carrying the same native RLS policy as every
-- other table, so no second role, no ownership transfer, and no SET ROLE are required. Removing
-- it also removes a real failure mode: migrations that GRANT to a role assumed to have been
-- created by hand fail with 42704 on any freshly provisioned database.
