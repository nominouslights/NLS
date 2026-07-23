using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverCredentialImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_content_type",
                schema: "drivers",
                table: "rm_driver_credentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                schema: "drivers",
                table: "rm_driver_credentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_content_type",
                schema: "drivers",
                table: "driver_credentials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "image_key",
                schema: "drivers",
                table: "driver_credentials",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_content_type",
                schema: "drivers",
                table: "rm_driver_credentials");

            migrationBuilder.DropColumn(
                name: "image_key",
                schema: "drivers",
                table: "rm_driver_credentials");

            migrationBuilder.DropColumn(
                name: "image_content_type",
                schema: "drivers",
                table: "driver_credentials");

            migrationBuilder.DropColumn(
                name: "image_key",
                schema: "drivers",
                table: "driver_credentials");
        }
    }
}
