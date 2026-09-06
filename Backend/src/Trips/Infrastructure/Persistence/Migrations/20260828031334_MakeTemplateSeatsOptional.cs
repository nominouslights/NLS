using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeTemplateSeatsOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "seats_capacity",
                schema: "trips",
                table: "schedule_templates",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "seats_capacity",
                schema: "trips",
                table: "rm_schedule_templates",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // ---- Hand-appended: backfill ----
            // Cargo-service templates never really had seats — the old NOT NULL column forced
            // dispatchers to invent a number. Null them out (both the aggregate table and its
            // projection mirror) so existing Cargo/Grocery templates land in the new model
            // without waiting for their next edit + reprojection.
            migrationBuilder.Sql(
                """
                UPDATE trips.schedule_templates
                    SET seats_capacity = NULL, seats_minimum = NULL
                    WHERE service_type IN ('Cargo', 'Grocery');

                UPDATE trips.rm_schedule_templates
                    SET seats_capacity = NULL, seats_minimum = NULL
                    WHERE service_type IN ('Cargo', 'Grocery');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "seats_capacity",
                schema: "trips",
                table: "schedule_templates",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "seats_capacity",
                schema: "trips",
                table: "rm_schedule_templates",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
