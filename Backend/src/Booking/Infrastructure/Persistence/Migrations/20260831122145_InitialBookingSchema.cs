using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NorthernLink.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBookingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "booking");

            migrationBuilder.CreateTable(
                name: "aggregate_snapshots",
                schema: "booking",
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
                name: "booking_days",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corridor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    passenger_minimum_override = table.Column<int>(type: "integer", nullable: true),
                    seat_capacity_override = table.Column<int>(type: "integer", nullable: true),
                    trip_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_days", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "booking_policies",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancellation_window_hours = table.Column<int>(type: "integer", nullable: false),
                    early_cancellation_penalty_cad = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    booking_cutoff_hours = table.Column<int>(type: "integer", nullable: false),
                    seat_hold_minutes = table.Column<int>(type: "integer", nullable: false),
                    default_passenger_minimum = table.Column<int>(type: "integer", nullable: false),
                    default_seat_capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    corridor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corridor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    service_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    pickup_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pickup_stop_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pickup_address_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dropoff_stop_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dropoff_stop_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    dropoff_address_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    payment_method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    payment_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    hold_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "corridor_booking_settings",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corridor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passenger_minimum = table.Column<int>(type: "integer", nullable: true),
                    seat_capacity = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corridor_booking_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "corridor_lookup",
                schema: "booking",
                columns: table => new
                {
                    corridor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    origin = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_corridor_lookup", x => x.corridor_id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    phone_digits = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_journal",
                schema: "booking",
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
                schema: "booking",
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
                schema: "booking",
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
                name: "booking_passengers",
                schema: "booking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    is_billing_customer = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_passengers", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_passengers_bookings_booking_id",
                        column: x => x.booking_id,
                        principalSchema: "booking",
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_aggregate_snapshots_tenant_id_aggregate_type_created_at_utc",
                schema: "booking",
                table: "aggregate_snapshots",
                columns: new[] { "tenant_id", "aggregate_type", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_days_tenant_id_corridor_id_service_date",
                schema: "booking",
                table: "booking_days",
                columns: new[] { "tenant_id", "corridor_id", "service_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_passengers_booking_id",
                schema: "booking",
                table: "booking_passengers",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_passengers_tenant_id_booking_id",
                schema: "booking",
                table: "booking_passengers",
                columns: new[] { "tenant_id", "booking_id" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_policies_tenant_id",
                schema: "booking",
                table: "booking_policies",
                column: "tenant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_tenant_id_corridor_id_service_date",
                schema: "booking",
                table: "bookings",
                columns: new[] { "tenant_id", "corridor_id", "service_date" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_tenant_id_customer_id",
                schema: "booking",
                table: "bookings",
                columns: new[] { "tenant_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "IX_corridor_booking_settings_tenant_id_corridor_id",
                schema: "booking",
                table: "corridor_booking_settings",
                columns: new[] { "tenant_id", "corridor_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_corridor_lookup_tenant_id",
                schema: "booking",
                table: "corridor_lookup",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_name",
                schema: "booking",
                table: "customers",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_tenant_id_phone_digits",
                schema: "booking",
                table: "customers",
                columns: new[] { "tenant_id", "phone_digits" });

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_event_id",
                schema: "booking",
                table: "event_journal",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_journal_tenant_id_aggregate_id_aggregate_version",
                schema: "booking",
                table: "event_journal",
                columns: new[] { "tenant_id", "aggregate_id", "aggregate_version" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_id",
                schema: "booking",
                table: "outbox_messages",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "booking",
                table: "outbox_messages",
                column: "position",
                filter: "dispatched_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                schema: "booking",
                table: "outbox_messages",
                column: "position",
                filter: "processing_status = 'Pending'");

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // The database half of the platform's non-negotiable tenant rule. The EF global
            // query filters in BookingDbContext are the API half; RLS is the backstop that
            // holds even for raw SQL or a buggy filter. FORCE makes the policy bind the table
            // owner too (migrations run as the app role, which owns these tables). The session
            // variable app.tenant_id is set on connection open by TenantSessionInterceptor;
            // the NULLIF form guards the pooled-connection case where Npgsql's DISCARD ALL
            // resets a custom GUC to the empty string rather than unset.
            //
            // Same policy shapes as the canonical Clients migration (20260719093056), minus
            // the projection-worker arms Booking doesn't need (no rm_* read models here —
            // query handlers derive from the write tables under the tenant policy):
            //
            // 1. Write tables (customers, bookings, booking_passengers, booking_days,
            //    booking_policies, corridor_booking_settings) get the tenant policy only —
            //    no system worker reads them.
            //
            // 2. event_journal and aggregate_snapshots get SELECT + INSERT policies ONLY —
            //    the app role physically cannot rewrite audit history.
            //
            // 3. outbox_messages gets the tenant policy plus a permissive app.is_system
            //    policy: consumer modules' OutboxPollingConsumers read producer outboxes
            //    cross-tenant on a system session (booking's own outbox stays empty until
            //    Booking publishes, but the table carries the standard contract from day one).
            //
            // 4. corridor_lookup is a Trips replica — same canonical block as
            //    trips.vehicle_lookup (tenant policy + separate permissive app.is_system
            //    policy). The integration handler writes it under the event's tenant pushed
            //    as the ambient tenant; request-path connections never set app.is_system.
            //
            // 5. projection_checkpoints is system-owned (no tenant_id) — system-only policy.
            //    Booking runs no projection worker today, so no session ever qualifies until
            //    one exists; the lockdown costs nothing and the shape stays uniform.
            migrationBuilder.Sql(
                """
                ALTER TABLE booking.customers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.customers FORCE ROW LEVEL SECURITY;
                CREATE POLICY customers_tenant_isolation ON booking.customers
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.bookings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.bookings FORCE ROW LEVEL SECURITY;
                CREATE POLICY bookings_tenant_isolation ON booking.bookings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.booking_passengers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.booking_passengers FORCE ROW LEVEL SECURITY;
                CREATE POLICY booking_passengers_tenant_isolation ON booking.booking_passengers
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.booking_days ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.booking_days FORCE ROW LEVEL SECURITY;
                CREATE POLICY booking_days_tenant_isolation ON booking.booking_days
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.booking_policies ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.booking_policies FORCE ROW LEVEL SECURITY;
                CREATE POLICY booking_policies_tenant_isolation ON booking.booking_policies
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.corridor_booking_settings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.corridor_booking_settings FORCE ROW LEVEL SECURITY;
                CREATE POLICY corridor_booking_settings_tenant_isolation ON booking.corridor_booking_settings
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.corridor_lookup ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.corridor_lookup FORCE ROW LEVEL SECURITY;
                CREATE POLICY corridor_lookup_tenant_isolation ON booking.corridor_lookup
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY corridor_lookup_system_access ON booking.corridor_lookup
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE booking.event_journal ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.event_journal FORCE ROW LEVEL SECURITY;
                CREATE POLICY event_journal_tenant_select ON booking.event_journal
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY event_journal_tenant_insert ON booking.event_journal
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.aggregate_snapshots ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.aggregate_snapshots FORCE ROW LEVEL SECURITY;
                CREATE POLICY aggregate_snapshots_tenant_select ON booking.aggregate_snapshots
                    FOR SELECT USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY aggregate_snapshots_tenant_insert ON booking.aggregate_snapshots
                    FOR INSERT WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

                ALTER TABLE booking.outbox_messages ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.outbox_messages FORCE ROW LEVEL SECURITY;
                CREATE POLICY outbox_messages_tenant_isolation ON booking.outbox_messages
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY outbox_messages_system_dispatch ON booking.outbox_messages
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE booking.projection_checkpoints ENABLE ROW LEVEL SECURITY;
                ALTER TABLE booking.projection_checkpoints FORCE ROW LEVEL SECURITY;
                CREATE POLICY projection_checkpoints_system ON booking.projection_checkpoints
                    USING (current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aggregate_snapshots",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "booking_days",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "booking_passengers",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "booking_policies",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "corridor_booking_settings",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "corridor_lookup",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "event_journal",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "projection_checkpoints",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "booking");
        }
    }
}
