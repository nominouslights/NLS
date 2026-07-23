using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBillingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "billing",
                columns: table => new
                {
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    state = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_aggregate_snapshots", x => new { x.aggregate_id, x.version });
                });

            migrationBuilder.CreateTable(
                name: "billable_trips",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trip_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: true),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    service_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    route_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    distance_km = table.Column<int>(type: "integer", nullable: false),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    round_trip_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billable_trips", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contract_snapshots",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    billing_model = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rate_per_round_trip_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    gst_applicable = table.Column<bool>(type: "boolean", nullable: false),
                    budget_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    billing_frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    net_terms_days = table.Column<int>(type: "integer", nullable: false),
                    default_po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contract_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_journal",
                schema: "billing",
                columns: table => new
                {
                    position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    aggregate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aggregate_version = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_journal", x => x.position);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    budget_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    net_terms_days = table.Column<int>(type: "integer", nullable: false),
                    gst_applicable = table.Column<bool>(type: "boolean", nullable: false),
                    gst_rate = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    qbo_invoice_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    qbo_sync_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    lines = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "billing",
                columns: table => new
                {
                    position = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    routing_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.position);
                });

            migrationBuilder.CreateTable(
                name: "projection_checkpoints",
                schema: "billing",
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

            migrationBuilder.CreateTable(
                name: "rm_invoices",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_name = table.Column<string>(type: "text", nullable: false),
                    contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    po_number = table.Column<string>(type: "text", nullable: true),
                    budget_code = table.Column<string>(type: "text", nullable: true),
                    net_terms_days = table.Column<int>(type: "integer", nullable: false),
                    gst_applicable = table.Column<bool>(type: "boolean", nullable: false),
                    gst_rate = table.Column<decimal>(type: "numeric", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paid_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    qbo_invoice_id = table.Column<string>(type: "text", nullable: true),
                    qbo_sync_status = table.Column<string>(type: "text", nullable: false),
                    subtotal_cad = table.Column<decimal>(type: "numeric", nullable: false),
                    gst_cad = table.Column<decimal>(type: "numeric", nullable: false),
                    total_cad = table.Column<decimal>(type: "numeric", nullable: false),
                    line_count = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_invoices", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "billing",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_billable_trips_tenant_id_client_id_service_date",
                schema: "billing",
                table: "billable_trips",
                columns: new[] { "tenant_id", "client_id", "service_date" });

            migrationBuilder.CreateIndex(
                name: "IX_billable_trips_tenant_id_invoice_id",
                schema: "billing",
                table: "billable_trips",
                columns: new[] { "tenant_id", "invoice_id" });

            migrationBuilder.CreateIndex(
                name: "IX_contract_snapshots_tenant_id_client_id",
                schema: "billing",
                table: "contract_snapshots",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "billing",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "billing",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id_client_id",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id_invoice_number",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "invoice_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_tenant_id_status",
                schema: "billing",
                table: "invoices",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "billing",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "billing",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_rm_invoices_tenant_id_client_id",
                schema: "billing",
                table: "rm_invoices",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_invoices_tenant_id_status",
                schema: "billing",
                table: "rm_invoices",
                columns: new[] { "tenant_id", "status" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filter in BillingDbContext is the API half; RLS is the backstop that holds
            // even for raw SQL or a buggy filter. FORCE binds the table owner too (migrations
            // run as the app role, which owns these tables, so without FORCE the policy would
            // not apply to the very role the API connects as; no GRANTs needed for the same
            // reason). NULLIF guards the pooled-connection case: Npgsql's pool reset
            // (DISCARD ALL) resets a previously-set custom GUC to the EMPTY STRING, not to
            // unset, so a bare ''::uuid cast would throw 22P02 on a reused connection.
            //
            // Policy shapes (the Identity/Trips pattern):
            // 1. Every table carries the tenant arm keyed on app.tenant_id (set on connection
            //    open by TenantSessionInterceptor) OR the app.is_system arm — the system arm is
            //    what lets the tenant-less projection worker and rebuilder span tenants. The
            //    replica tables (contract_snapshots, billable_trips) are written by the
            //    integration-event consumers, whose handlers push the event's tenant into
            //    AmbientTenant so the session variable follows the payload — the tenant arm
            //    covers them.
            // 2. event_journal and aggregate_snapshots get SELECT + INSERT policies ONLY — the
            //    app role physically cannot rewrite audit history.
            // 3. outbox_messages needs UPDATE (mark dispatched) by the OutboxDispatcher, a
            //    background service with no tenant — a second permissive policy keyed on
            //    app.is_system lets it through; request-path connections never set that
            //    variable, so tenant isolation holds for them.
            // 4. projection_checkpoints is system-owned and spans tenants (no tenant_id) —
            //    system-only policy.
            migrationBuilder.Sql(
                """
                ALTER TABLE billing.invoices ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.invoices FORCE ROW LEVEL SECURITY;
                CREATE POLICY invoices_tenant_isolation ON billing.invoices
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE billing.contract_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.contract_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY contract_snapshots_tenant_isolation ON billing.contract_snapshots
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE billing.billable_trips ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.billable_trips FORCE ROW LEVEL SECURITY;
                CREATE POLICY billable_trips_tenant_isolation ON billing.billable_trips
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE billing.rm_invoices ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.rm_invoices FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_invoices_tenant_isolation ON billing.rm_invoices
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE billing.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON billing.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');
                CREATE POLICY event_journal_tenant_insert ON billing.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE billing.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON billing.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');
                CREATE POLICY aggregate_snapshots_tenant_insert ON billing.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                        OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE billing.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON billing.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON billing.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE billing.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE billing.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON billing.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "billable_trips",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "contract_snapshots",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "rm_invoices",
                schema: "billing");
        }
    }
}
