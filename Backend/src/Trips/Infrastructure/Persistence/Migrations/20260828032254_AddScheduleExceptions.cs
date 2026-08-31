using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleExceptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rm_schedule_exceptions",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    return_departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_schedule_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_exceptions",
                schema: "trips",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    return_departure_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_schedule_exceptions_schedule_templates_schedule_template_id",
                        column: x => x.schedule_template_id,
                        principalSchema: "trips",
                        principalTable: "schedule_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rm_schedule_exceptions_tenant_id_schedule_template_id_date",
                schema: "trips",
                table: "rm_schedule_exceptions",
                columns: new[] { "tenant_id", "schedule_template_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_schedule_template_id",
                schema: "trips",
                table: "schedule_exceptions",
                column: "schedule_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_exceptions_tenant_id_schedule_template_id_date",
                schema: "trips",
                table: "schedule_exceptions",
                columns: new[] { "tenant_id", "schedule_template_id", "date" },
                unique: true);

            // ---- Hand-appended: Postgres Row-Level Security (dual tenant enforcement) ----
            // Same idioms as AddTripPlanning. The write table gets the tenant policy PLUS a
            // separate permissive app.is_system policy — the generation worker Includes
            // exceptions when it loads templates on its pinned system session, exactly like
            // trips.schedule_templates itself. The rm_ table uses the single OR-arm policy
            // (the rm_trips shape): the tenant-less projection worker/rebuilder writes it
            // across tenants. NULLIF guards the pooled-connection empty-string case; FORCE
            // binds the table owner too.
            migrationBuilder.Sql(
                """
                ALTER TABLE trips.schedule_exceptions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.schedule_exceptions FORCE ROW LEVEL SECURITY;
                CREATE POLICY schedule_exceptions_tenant_isolation ON trips.schedule_exceptions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY schedule_exceptions_system_access ON trips.schedule_exceptions
                    USING (current_setting('app.is_system', true) = 'true');

                ALTER TABLE trips.rm_schedule_exceptions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE trips.rm_schedule_exceptions FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_schedule_exceptions_tenant_isolation ON trips.rm_schedule_exceptions
                    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid
                           OR current_setting('app.is_system', true) = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rm_schedule_exceptions",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "schedule_exceptions",
                schema: "trips");
        }
    }
}
