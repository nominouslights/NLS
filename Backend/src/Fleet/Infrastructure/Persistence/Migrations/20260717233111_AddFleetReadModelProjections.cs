using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetReadModelProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Projection checkpoint — one row per module worker, system-owned (no tenant_id).
            // EF-generated CreateTable; RLS is hand-appended below.
            migrationBuilder.CreateTable(
                name: "projection_checkpoints",
                schema: "fleet",
                columns: table => new
                {
                    projection_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_position = table.Column<long>(type: "bigint", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projection_checkpoints", x => x.projection_name);
                });

            // ---- Hand-appended: read-side materialized views + tenant backstop ----
            // EF can't model matviews/views, so the read side is raw SQL. See the plan and
            // AddFleetAuditInfrastructure for the RLS/NULLIF idioms reused here.

            // Half-to-even rounding to match C# Math.Round (MidpointRounding.ToEven) exactly.
            // Postgres round(numeric, int) is half-away-from-zero, which diverges from the
            // Vehicle aggregate's depreciation properties on exact midpoints — this keeps the
            // matview values bit-for-bit identical to the values the API returned before.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION fleet.round_even(value numeric, scale int)
                RETURNS numeric
                LANGUAGE plpgsql
                IMMUTABLE
                AS $$
                DECLARE
                    factor  numeric := power(10::numeric, scale);
                    scaled  numeric := value * factor;
                    lowered numeric := floor(scaled);
                    frac    numeric := scaled - lowered;
                    rounded numeric;
                BEGIN
                    IF frac < 0.5 THEN
                        rounded := lowered;
                    ELSIF frac > 0.5 THEN
                        rounded := lowered + 1;
                    ELSIF mod(lowered, 2::numeric) = 0 THEN
                        rounded := lowered;      -- exact half: floor is already even
                    ELSE
                        rounded := lowered + 1;  -- exact half: step up to the even neighbour
                    END IF;
                    RETURN rounded / factor;
                END;
                $$;
                """);

            // mv_vehicles carries the four derived columns computed in SQL. end_of_life_km > 0
            // is guaranteed by the aggregate's validation, so the divisions are safe.
            migrationBuilder.Sql(
                """
                CREATE MATERIALIZED VIEW fleet.mv_vehicles AS
                SELECT
                    id, tenant_id, unit_number, vin, make, model, year, seating_capacity,
                    licence_plate, required_licence_class, status, status_reason, odometer_km,
                    acquisition_cost_cad, end_of_life_km, sale_price_cad, disposed_at_utc,
                    created_at_utc, updated_at_utc, version,
                    (seating_capacity >= 11) AS requires_periodic_inspection,
                    fleet.round_even(
                        acquisition_cost_cad
                        * greatest(0::numeric, 1 - odometer_km::numeric / end_of_life_km), 2)
                        AS current_value_cad,
                    greatest(0, end_of_life_km - odometer_km) AS remaining_km,
                    least(100::numeric,
                        fleet.round_even(odometer_km::numeric / end_of_life_km * 100, 1))
                        AS life_used_pct
                FROM fleet.vehicles
                WITH NO DATA;

                CREATE MATERIALIZED VIEW fleet.mv_retirement_certificates AS
                    SELECT * FROM fleet.retirement_certificates WITH NO DATA;

                CREATE MATERIALIZED VIEW fleet.mv_shops AS
                    SELECT * FROM fleet.shops WITH NO DATA;

                CREATE MATERIALIZED VIEW fleet.mv_vehicle_documents AS
                    SELECT * FROM fleet.vehicle_documents WITH NO DATA;

                CREATE MATERIALIZED VIEW fleet.mv_service_records AS
                    SELECT * FROM fleet.service_records WITH NO DATA;

                CREATE MATERIALIZED VIEW fleet.mv_work_orders AS
                    SELECT * FROM fleet.work_orders WITH NO DATA;

                CREATE MATERIALIZED VIEW fleet.mv_vehicle_inspections AS
                    SELECT * FROM fleet.vehicle_inspections WITH NO DATA;
                """);

            // Unique index on id per matview — REFRESH ... CONCURRENTLY requires one.
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX ix_mv_vehicles_id ON fleet.mv_vehicles (id);
                CREATE UNIQUE INDEX ix_mv_retirement_certificates_id ON fleet.mv_retirement_certificates (id);
                CREATE UNIQUE INDEX ix_mv_shops_id ON fleet.mv_shops (id);
                CREATE UNIQUE INDEX ix_mv_vehicle_documents_id ON fleet.mv_vehicle_documents (id);
                CREATE UNIQUE INDEX ix_mv_service_records_id ON fleet.mv_service_records (id);
                CREATE UNIQUE INDEX ix_mv_work_orders_id ON fleet.mv_work_orders (id);
                CREATE UNIQUE INDEX ix_mv_vehicle_inspections_id ON fleet.mv_vehicle_inspections (id);
                """);

            // Base tables feed the matviews, but a matview refresh runs on a system session
            // with no tenant — the tenant-only RLS policies would show it zero rows. Add an
            // app.is_system read-bypass policy (OR-ed with the existing tenant policy) to each
            // base table, and to event_journal (the worker reads its cursor the same way).
            // Only OutboxDispatcher and the projection worker ever set app.is_system, both on
            // pinned system connections, so tenant isolation for request sessions is untouched.
            migrationBuilder.Sql(
                """
                CREATE POLICY vehicles_system_read ON fleet.vehicles
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY retirement_certificates_system_read ON fleet.retirement_certificates
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY shops_system_read ON fleet.shops
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY vehicle_documents_system_read ON fleet.vehicle_documents
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY service_records_system_read ON fleet.service_records
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY work_orders_system_read ON fleet.work_orders
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY vehicle_inspections_system_read ON fleet.vehicle_inspections
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                CREATE POLICY event_journal_system_read ON fleet.event_journal
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');
                """);

            // projection_checkpoints — system-owned, spans tenants. FORCE RLS even for the
            // owning app role; a single policy admits only the system session (worker), which
            // both reads the cursor and writes it. WITH CHECK defaults to the USING expression.
            migrationBuilder.Sql(
                """
                ALTER TABLE fleet.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE fleet.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON fleet.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);

            // Two-role tenant backstop. Each matview + its wrapper view end up owned by
            // northernlink_projector; the app role gets SELECT on the wrapper view ONLY and no
            // privilege on the matview (a raw SELECT on mv_* as app is denied by table grant,
            // because app is a NOINHERIT member of projector — it can SET ROLE to refresh but
            // does not inherit the owner's privileges). The wrapper is security_barrier so the
            // app can't leak other tenants' rows, and it filters by the tenant session variable
            // — the DB half of dual enforcement, paired with the read model's EF query filter.
            //
            // Ordering matters: the wrappers are created while app still owns the matviews (so
            // app can read them at CREATE VIEW time), THEN ownership of both is handed to the
            // projector and app's direct matview privilege is revoked. The projector also needs
            // SELECT on the base tables (a refresh runs the matview query as the projector owner)
            // and USAGE + CREATE on the schema (to receive ownership and resolve names).
            migrationBuilder.Sql(
                """
                GRANT USAGE, CREATE ON SCHEMA fleet TO northernlink_projector;

                GRANT SELECT ON fleet.vehicles TO northernlink_projector;
                GRANT SELECT ON fleet.retirement_certificates TO northernlink_projector;
                GRANT SELECT ON fleet.shops TO northernlink_projector;
                GRANT SELECT ON fleet.vehicle_documents TO northernlink_projector;
                GRANT SELECT ON fleet.service_records TO northernlink_projector;
                GRANT SELECT ON fleet.work_orders TO northernlink_projector;
                GRANT SELECT ON fleet.vehicle_inspections TO northernlink_projector;

                CREATE VIEW fleet.v_vehicles WITH (security_barrier = true) AS
                    SELECT * FROM fleet.mv_vehicles
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                CREATE VIEW fleet.v_retirement_certificates WITH (security_barrier = true) AS
                    SELECT * FROM fleet.mv_retirement_certificates
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                CREATE VIEW fleet.v_shops WITH (security_barrier = true) AS
                    SELECT * FROM fleet.mv_shops
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                CREATE VIEW fleet.v_vehicle_documents WITH (security_barrier = true) AS
                    SELECT * FROM fleet.mv_vehicle_documents
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                CREATE VIEW fleet.v_service_records WITH (security_barrier = true) AS
                    SELECT * FROM fleet.mv_service_records
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                CREATE VIEW fleet.v_work_orders WITH (security_barrier = true) AS
                    SELECT * FROM fleet.mv_work_orders
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;
                CREATE VIEW fleet.v_vehicle_inspections WITH (security_barrier = true) AS
                    SELECT * FROM fleet.mv_vehicle_inspections
                    WHERE tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid;

                ALTER MATERIALIZED VIEW fleet.mv_vehicles OWNER TO northernlink_projector;
                ALTER MATERIALIZED VIEW fleet.mv_retirement_certificates OWNER TO northernlink_projector;
                ALTER MATERIALIZED VIEW fleet.mv_shops OWNER TO northernlink_projector;
                ALTER MATERIALIZED VIEW fleet.mv_vehicle_documents OWNER TO northernlink_projector;
                ALTER MATERIALIZED VIEW fleet.mv_service_records OWNER TO northernlink_projector;
                ALTER MATERIALIZED VIEW fleet.mv_work_orders OWNER TO northernlink_projector;
                ALTER MATERIALIZED VIEW fleet.mv_vehicle_inspections OWNER TO northernlink_projector;

                ALTER VIEW fleet.v_vehicles OWNER TO northernlink_projector;
                ALTER VIEW fleet.v_retirement_certificates OWNER TO northernlink_projector;
                ALTER VIEW fleet.v_shops OWNER TO northernlink_projector;
                ALTER VIEW fleet.v_vehicle_documents OWNER TO northernlink_projector;
                ALTER VIEW fleet.v_service_records OWNER TO northernlink_projector;
                ALTER VIEW fleet.v_work_orders OWNER TO northernlink_projector;
                ALTER VIEW fleet.v_vehicle_inspections OWNER TO northernlink_projector;

                -- Grant app SELECT on the wrappers AS the projector (their new owner) — done via
                -- SET ROLE because app is a NOINHERIT member and so can no longer grant on objects
                -- it just handed over. No REVOKE on the matviews is needed: the ownership handover
                -- already stripped app's implicit privileges, and NOINHERIT means it inherits none
                -- from the projector — so a raw SELECT on mv_* as app is denied, wrappers only.
                SET ROLE northernlink_projector;
                GRANT SELECT ON fleet.v_vehicles TO northernlink_app;
                GRANT SELECT ON fleet.v_retirement_certificates TO northernlink_app;
                GRANT SELECT ON fleet.v_shops TO northernlink_app;
                GRANT SELECT ON fleet.v_vehicle_documents TO northernlink_app;
                GRANT SELECT ON fleet.v_service_records TO northernlink_app;
                GRANT SELECT ON fleet.v_work_orders TO northernlink_app;
                GRANT SELECT ON fleet.v_vehicle_inspections TO northernlink_app;
                RESET ROLE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP VIEW IF EXISTS fleet.v_vehicles;
                DROP VIEW IF EXISTS fleet.v_retirement_certificates;
                DROP VIEW IF EXISTS fleet.v_shops;
                DROP VIEW IF EXISTS fleet.v_vehicle_documents;
                DROP VIEW IF EXISTS fleet.v_service_records;
                DROP VIEW IF EXISTS fleet.v_work_orders;
                DROP VIEW IF EXISTS fleet.v_vehicle_inspections;

                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_vehicles;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_retirement_certificates;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_shops;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_vehicle_documents;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_service_records;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_work_orders;
                DROP MATERIALIZED VIEW IF EXISTS fleet.mv_vehicle_inspections;

                DROP FUNCTION IF EXISTS fleet.round_even(numeric, int);

                DROP POLICY IF EXISTS vehicles_system_read ON fleet.vehicles;
                DROP POLICY IF EXISTS retirement_certificates_system_read ON fleet.retirement_certificates;
                DROP POLICY IF EXISTS shops_system_read ON fleet.shops;
                DROP POLICY IF EXISTS vehicle_documents_system_read ON fleet.vehicle_documents;
                DROP POLICY IF EXISTS service_records_system_read ON fleet.service_records;
                DROP POLICY IF EXISTS work_orders_system_read ON fleet.work_orders;
                DROP POLICY IF EXISTS vehicle_inspections_system_read ON fleet.vehicle_inspections;
                DROP POLICY IF EXISTS event_journal_system_read ON fleet.event_journal;
                """);

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "fleet");
        }
    }
}
