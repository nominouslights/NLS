using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Compliance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialComplianceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "compliance");

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "compliance",
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
                name: "compliance_records",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_record_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    anchor_dates = table.Column<string>(type: "jsonb", nullable: false),
                    retain_until = table.Column<DateOnly>(type: "date", nullable: true),
                    retention_pending = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    holds = table.Column<string>(type: "jsonb", nullable: false),
                    supporting_links = table.Column<string>(type: "jsonb", nullable: false),
                    print_template_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    document_version = table.Column<int>(type: "integer", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    regulatory_basis_keys = table.Column<string>(type: "jsonb", nullable: false),
                    registered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disposed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compliance_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "destruction_log",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    record_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    disposed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disposed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_destruction_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_series",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_sequence = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_journal",
                schema: "compliance",
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
                schema: "compliance",
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
                schema: "compliance",
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
                name: "regulatory_basis",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    jurisdiction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    instrument = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    citation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    quotation = table.Column<string>(type: "text", nullable: true),
                    plain_explanation = table.Column<string>(type: "text", nullable: false),
                    consequence = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    plan_citation_note = table.Column<string>(type: "text", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    rule_set_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_basis", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_compliance_records",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    document_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_module = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    source_record_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    anchor_dates = table.Column<string>(type: "jsonb", nullable: false),
                    retain_until = table.Column<DateOnly>(type: "date", nullable: true),
                    retention_pending = table.Column<bool>(type: "boolean", nullable: false),
                    is_permanent = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_held = table.Column<bool>(type: "boolean", nullable: false),
                    holds = table.Column<string>(type: "jsonb", nullable: false),
                    supporting_links = table.Column<string>(type: "jsonb", nullable: false),
                    print_template_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    document_version = table.Column<int>(type: "integer", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    regulatory_basis_keys = table.Column<string>(type: "jsonb", nullable: false),
                    registered_by = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disposed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_compliance_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rule_sets",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    jurisdiction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    hos = table.Column<string>(type: "jsonb", nullable: false),
                    retention = table.Column<string>(type: "jsonb", nullable: false),
                    inspection = table.Column<string>(type: "jsonb", nullable: false),
                    driver_qualification = table.Column<string>(type: "jsonb", nullable: false),
                    filing = table.Column<string>(type: "jsonb", nullable: false),
                    thresholds = table.Column<string>(type: "jsonb", nullable: false),
                    insurance = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_compliance_settings",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carrier_legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    nsc_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    document_prefix = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    principal_place_of_business = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    file_location_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_compliance_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_rule_set_applications",
                schema: "compliance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_set_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applied_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    change_notice = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    applied_by = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_rule_set_applications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "compliance",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_tenant_id_document_number",
                schema: "compliance",
                table: "compliance_records",
                columns: new[] { "tenant_id", "document_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_tenant_id_source_module_source_record_id",
                schema: "compliance",
                table: "compliance_records",
                columns: new[] { "tenant_id", "source_module", "source_record_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compliance_records_tenant_id_status_retain_until",
                schema: "compliance",
                table: "compliance_records",
                columns: new[] { "tenant_id", "status", "retain_until" });

            migrationBuilder.CreateIndex(
                name: "IX_destruction_log_tenant_id_disposed_at_utc",
                schema: "compliance",
                table: "destruction_log",
                columns: new[] { "tenant_id", "disposed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_document_series_tenant_id_series_year",
                schema: "compliance",
                table: "document_series",
                columns: new[] { "tenant_id", "series", "year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "compliance",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "compliance",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "compliance",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "compliance",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "compliance",
                table: "outbox_messages",
                column: "position",
                filter: "processing_status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_basis_instrument",
                schema: "compliance",
                table: "regulatory_basis",
                column: "instrument");

            migrationBuilder.CreateIndex(
                name: "IX_regulatory_basis_key",
                schema: "compliance",
                table: "regulatory_basis",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_compliance_records_tenant_id_record_type",
                schema: "compliance",
                table: "rm_compliance_records",
                columns: new[] { "tenant_id", "record_type" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_compliance_records_tenant_id_status_retain_until",
                schema: "compliance",
                table: "rm_compliance_records",
                columns: new[] { "tenant_id", "status", "retain_until" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_compliance_records_tenant_id_subject_kind_subject_id",
                schema: "compliance",
                table: "rm_compliance_records",
                columns: new[] { "tenant_id", "subject_kind", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rule_sets_jurisdiction_version",
                schema: "compliance",
                table: "rule_sets",
                columns: new[] { "jurisdiction", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_compliance_settings_tenant_id",
                schema: "compliance",
                table: "tenant_compliance_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_rule_set_applications_tenant_id_applied_at_utc",
                schema: "compliance",
                table: "tenant_rule_set_applications",
                columns: new[] { "tenant_id", "applied_at_utc" });

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filter in ComplianceDbContext is the API half; RLS is the backstop that
            // holds even for raw SQL or a buggy filter. FORCE makes the policy bind the table
            // owner too (migrations run as the app role, which owns these tables). The session
            // variable app.tenant_id is set on connection open by TenantSessionInterceptor;
            // the NULLIF form guards the pooled-connection case where Npgsql's DISCARD ALL
            // resets a custom GUC to the empty string rather than unset.
            //
            // Same policy shapes as the Budgeting initial migration, one per table kind:
            //
            // 1. Tenant write tables (compliance_records, document_series,
            //    tenant_compliance_settings, tenant_rule_set_applications, destruction_log) get
            //    the tenant policy. compliance_records additionally gets an app.is_system SELECT
            //    bypass: the tenant-less projection worker reads write-side rows to build the
            //    read model, and the RetentionWorker enumerates tenants across the table.
            //
            // 2. event_journal and aggregate_snapshots get SELECT + INSERT policies ONLY —
            //    the app role physically cannot rewrite audit history. The journal also gets
            //    the app.is_system SELECT bypass (the worker's poll cursor spans tenants).
            //
            // 3. outbox_messages needs UPDATE (mark dispatched / mark processed) by the
            //    OutboxDispatcher and by other modules' OutboxPollingConsumers — background
            //    services with no tenant. A second permissive policy keyed on app.is_system lets
            //    them through; request-path connections never set that variable.
            //
            // 4. rm_compliance_records carries one policy: tenant OR app.is_system, so the
            //    worker and rebuilder can write across tenants.
            //
            // 5. projection_checkpoints is system-owned (no tenant_id) — system-only policy.
            //
            // 6. rule_sets and regulatory_basis are REFERENCE DATA with no tenant_id: the law is
            //    the same for every carrier in the jurisdiction, so every session may read them
            //    (USING (true) FOR SELECT) and only the platform's system session may write them
            //    (publishing a rule-set version, seeding the catalogue — RuleSetRepository and
            //    RegulatoryBasisRepository run those writes under app.is_system, see
            //    SystemSession.cs). RLS is still ENABLED and FORCED on both so a tenant session
            //    can never insert, rewrite or delete the law — the tables are read-only to every
            //    request, and the write arm is the deliberate, explicit exception.
            migrationBuilder.Sql(
                """
                ALTER TABLE compliance.compliance_records ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.compliance_records FORCE ROW LEVEL SECURITY;
                CREATE POLICY compliance_records_tenant_isolation ON compliance.compliance_records
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY compliance_records_system_read ON compliance.compliance_records
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE compliance.document_series ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.document_series FORCE ROW LEVEL SECURITY;
                CREATE POLICY document_series_tenant_isolation ON compliance.document_series
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.tenant_compliance_settings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.tenant_compliance_settings FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_compliance_settings_tenant_isolation ON compliance.tenant_compliance_settings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.tenant_rule_set_applications ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.tenant_rule_set_applications FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_rule_set_applications_tenant_isolation ON compliance.tenant_rule_set_applications
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.destruction_log ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.destruction_log FORCE ROW LEVEL SECURITY;
                CREATE POLICY destruction_log_tenant_isolation ON compliance.destruction_log
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON compliance.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_tenant_insert ON compliance.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_system_read ON compliance.event_journal
                    FOR SELECT USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE compliance.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON compliance.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY aggregate_snapshots_tenant_insert ON compliance.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE compliance.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON compliance.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON compliance.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE compliance.rm_compliance_records ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.rm_compliance_records FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_compliance_records_tenant_isolation ON compliance.rm_compliance_records
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');

                ALTER TABLE compliance.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON compliance.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE compliance.rule_sets ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.rule_sets FORCE ROW LEVEL SECURITY;
                CREATE POLICY rule_sets_reference_read ON compliance.rule_sets
                    FOR SELECT USING (true);
                CREATE POLICY rule_sets_system_write ON compliance.rule_sets
                    FOR ALL USING (current_setting('app.is_system', true) = 'true')
                    WITH CHECK (current_setting('app.is_system', true) = 'true');

                ALTER TABLE compliance.regulatory_basis ENABLE ROW LEVEL SECURITY;
                ALTER TABLE compliance.regulatory_basis FORCE ROW LEVEL SECURITY;
                CREATE POLICY regulatory_basis_reference_read ON compliance.regulatory_basis
                    FOR SELECT USING (true);
                CREATE POLICY regulatory_basis_system_write ON compliance.regulatory_basis
                    FOR ALL USING (current_setting('app.is_system', true) = 'true')
                    WITH CHECK (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "compliance_records",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "destruction_log",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "document_series",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "regulatory_basis",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "rm_compliance_records",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "rule_sets",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "tenant_compliance_settings",
                schema: "compliance");

            migrationBuilder.DropTable(
                name: "tenant_rule_set_applications",
                schema: "compliance");
        }
    }
}
