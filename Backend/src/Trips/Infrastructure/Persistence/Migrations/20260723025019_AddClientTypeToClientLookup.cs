using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientTypeToClientLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "service_type",
                schema: "trips",
                table: "client_lookup",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "trips",
                table: "client_lookup",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Client");

            // Drop default after backfill
            migrationBuilder.Sql("ALTER TABLE trips.client_lookup ALTER COLUMN type DROP DEFAULT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                schema: "trips",
                table: "client_lookup");

            migrationBuilder.AlterColumn<string>(
                name: "service_type",
                schema: "trips",
                table: "client_lookup",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
