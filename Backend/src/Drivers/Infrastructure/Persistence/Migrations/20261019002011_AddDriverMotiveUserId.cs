using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverMotiveUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "motive_user_id",
                schema: "drivers",
                table: "rm_drivers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motive_user_id",
                schema: "drivers",
                table: "drivers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_drivers_tenant_id_motive_user_id",
                schema: "drivers",
                table: "drivers",
                columns: new[] { "tenant_id", "motive_user_id" },
                unique: true,
                filter: "motive_user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_drivers_tenant_id_motive_user_id",
                schema: "drivers",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "motive_user_id",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "motive_user_id",
                schema: "drivers",
                table: "drivers");
        }
    }
}
