using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Clients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContractTaxFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "gst_applicable",
                schema: "clients",
                table: "rm_contracts");

            migrationBuilder.DropColumn(
                name: "active_contract_gst_applicable",
                schema: "clients",
                table: "rm_clients");

            migrationBuilder.DropColumn(
                name: "gst_applicable",
                schema: "clients",
                table: "contracts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "gst_applicable",
                schema: "clients",
                table: "rm_contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "active_contract_gst_applicable",
                schema: "clients",
                table: "rm_clients",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "gst_applicable",
                schema: "clients",
                table: "contracts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
