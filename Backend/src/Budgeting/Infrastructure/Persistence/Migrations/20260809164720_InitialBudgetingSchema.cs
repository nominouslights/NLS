using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Budgeting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBudgetingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "budgeting");

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "budgeting",
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
                name: "budget_periods",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granularity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    label = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_budget_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_journal",
                schema: "budgeting",
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
                schema: "budgeting",
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
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processing_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Pending"),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processing_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    processing_last_error = table.Column<string>(type: "text", nullable: true),
                    processing_next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.position);
                });

            migrationBuilder.CreateTable(
                name: "projection_checkpoints",
                schema: "budgeting",
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
                name: "rm_budget_periods",
                schema: "budgeting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    granularity = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_budget_periods", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "budgeting",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_budget_periods_tenant_id_granularity_year_ordinal",
                schema: "budgeting",
                table: "budget_periods",
                columns: new[] { "tenant_id", "granularity", "year", "ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_budget_periods_tenant_id_starts_on",
                schema: "budgeting",
                table: "budget_periods",
                columns: new[] { "tenant_id", "starts_on" });

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "budgeting",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "budgeting",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "budgeting",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "budgeting",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "budgeting",
                table: "outbox_messages",
                column: "position",
                filter: "processing_status = 'Pending'");

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filter in BudgetingDbContext is the API half; RLS is the backstop that
            // holds even for raw SQL or a buggy filter. FORCE makes the policy bind the table
            // owner too (migrations run as the app role, which owns these tables). The session
            // variable app.tenant_id is set on connection open by TenantSessionInterceptor;
            // the NULLIF form guards the pooled-connection case where Npgsql's DISCARD ALL
            // resets a custom GUC to the empty string rather than unset.
            //
            // Same policy shapes as the Notifications initial migration (this module starts
            // with the projection read side in place, so the system-read arms land here rather
            // than in a later migration):
            //
            // 1. The write table (budget_periods) gets the tenant policy plus an
            //    app.is_system SELECT bypass — the tenant-less projection worker reads
            //    write-side rows to build the read models.
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
                ALTER TABLE budgeting.budget_periods ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.budget_periods FORCE ROW LEVEL SECURITY;
                CREATE POLICY budget_periods_tenant_isolation ON budgeting.budget_periods
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY budget_periods_system_read ON budgeting.budget_periods
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON budgeting.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_tenant_insert ON budgeting.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_system_read ON budgeting.event_journal
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON budgeting.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY aggregate_snapshots_tenant_insert ON budgeting.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE budgeting.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON budgeting.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON budgeting.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.rm_budget_periods ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.rm_budget_periods FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_budget_periods_tenant_isolation ON budgeting.rm_budget_periods
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE budgeting.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE budgeting.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON budgeting.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "budget_periods",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "budgeting");

            migrationBuilder.DropTable(
                name: "rm_budget_periods",
                schema: "budgeting");
        }
    }
}
