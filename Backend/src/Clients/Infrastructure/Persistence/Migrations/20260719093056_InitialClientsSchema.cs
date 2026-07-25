using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Clients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialClientsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clients");

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "clients",
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
                name: "clients",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contracts",
                schema: "clients",
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
                    billing_frequency = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    net_terms_days = table.Column<int>(type: "integer", nullable: false),
                    default_po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contracts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_journal",
                schema: "clients",
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
                name: "outbox_messages",
                schema: "clients",
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
                schema: "clients",
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
                name: "purchase_orders",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issued = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    amount_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_clients",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    service_type = table.Column<string>(type: "text", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    active_contract_id = table.Column<Guid>(type: "uuid", nullable: true),
                    active_contract_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    active_contract_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    active_contract_billing_model = table.Column<string>(type: "text", nullable: true),
                    active_contract_rate_per_round_trip_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    active_contract_gst_applicable = table.Column<bool>(type: "boolean", nullable: true),
                    active_contract_budget_code = table.Column<string>(type: "text", nullable: true),
                    active_contract_billing_frequency = table.Column<string>(type: "text", nullable: true),
                    active_contract_net_terms_days = table.Column<int>(type: "integer", nullable: true),
                    active_contract_default_po_number = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_contracts",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_name = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    billing_model = table.Column<string>(type: "text", nullable: false),
                    rate_per_round_trip_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    gst_applicable = table.Column<bool>(type: "boolean", nullable: false),
                    budget_code = table.Column<string>(type: "text", nullable: true),
                    billing_frequency = table.Column<string>(type: "text", nullable: false),
                    net_terms_days = table.Column<int>(type: "integer", nullable: false),
                    default_po_number = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_contracts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_purchase_orders",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    po_number = table.Column<string>(type: "text", nullable: false),
                    issued = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry = table.Column<DateOnly>(type: "date", nullable: true),
                    amount_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_purchase_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "clients",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_clients_tenant_id_name",
                schema: "clients",
                table: "clients",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_contracts_tenant_id_client_id",
                schema: "clients",
                table: "contracts",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "clients",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "clients",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "clients",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "clients",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_tenant_id_client_id",
                schema: "clients",
                table: "purchase_orders",
                columns: new[] { "tenant_id", "client_id" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filter in ClientsDbContext is the API half; RLS is the backstop that holds
            // even for raw SQL or a buggy filter. FORCE makes the policy bind the table
            // owner too (migrations run as the app role, which owns these tables). The session
            // variable app.tenant_id is set on connection open by TenantSessionInterceptor;
            // the NULLIF form guards the pooled-connection case where Npgsql's DISCARD ALL
            // resets a custom GUC to the empty string rather than unset.
            //
            // Same policy shapes as Trips' migrations (this module starts with the projection
            // read side in place, so the system-read arms land here rather than in a later
            // migration):
            //
            // 1. Write tables (clients, contracts, purchase_orders) get the tenant policy
            //    plus an app.is_system SELECT bypass — the tenant-less projection worker
            //    reads write-side rows to build the read models.
            //
            // 2. event_journal and aggregate_snapshots get SELECT + INSERT policies ONLY —
            //    the app role physically cannot rewrite audit history. The journal also gets
            //    the app.is_system SELECT bypass (the worker's poll cursor spans tenants).
            //
            // 3. outbox_messages needs UPDATE (mark dispatched) by the OutboxDispatcher,
            //    a background service with no tenant. A second permissive policy keyed on
            //    app.is_system lets it through; request-path connections never set that
            //    variable, so tenant isolation holds for them.
            //
            // 4. rm_* projection tables carry one policy: tenant OR app.is_system, so the
            //    worker and rebuilder can write across tenants.
            //
            // 5. projection_checkpoints is system-owned (no tenant_id) — system-only policy.
            migrationBuilder.Sql(
                """
                ALTER TABLE clients.clients ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.clients FORCE ROW LEVEL SECURITY;
                CREATE POLICY clients_tenant_isolation ON clients.clients
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY clients_system_read ON clients.clients
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.contracts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.contracts FORCE ROW LEVEL SECURITY;
                CREATE POLICY contracts_tenant_isolation ON clients.contracts
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY contracts_system_read ON clients.contracts
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.purchase_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.purchase_orders FORCE ROW LEVEL SECURITY;
                CREATE POLICY purchase_orders_tenant_isolation ON clients.purchase_orders
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY purchase_orders_system_read ON clients.purchase_orders
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON clients.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_tenant_insert ON clients.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_system_read ON clients.event_journal
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON clients.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY aggregate_snapshots_tenant_insert ON clients.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE clients.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON clients.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON clients.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.rm_clients ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.rm_clients FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_clients_tenant_isolation ON clients.rm_clients
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.rm_contracts ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.rm_contracts FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_contracts_tenant_isolation ON clients.rm_contracts
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.rm_purchase_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.rm_purchase_orders FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_purchase_orders_tenant_isolation ON clients.rm_purchase_orders
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE clients.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON clients.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "clients",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "contracts",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "purchase_orders",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "rm_clients",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "rm_contracts",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "rm_purchase_orders",
                schema: "clients");
        }
    }
}
