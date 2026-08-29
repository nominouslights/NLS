using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Clients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddContactReceivesAccrualsReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "receives_accruals_reports",
                schema: "clients",
                table: "rm_client_contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "receives_accruals_reports",
                schema: "clients",
                table: "client_contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "receives_accruals_reports",
                schema: "clients",
                table: "rm_client_contacts");

            migrationBuilder.DropColumn(
                name: "receives_accruals_reports",
                schema: "clients",
                table: "client_contacts");
        }
    }
}
