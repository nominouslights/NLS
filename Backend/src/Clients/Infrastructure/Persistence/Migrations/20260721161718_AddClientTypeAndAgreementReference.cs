using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Clients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientTypeAndAgreementReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "service_type",
                schema: "clients",
                table: "rm_clients",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "agreement_reference",
                schema: "clients",
                table: "rm_clients",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "clients",
                table: "rm_clients",
                type: "text",
                nullable: false,
                defaultValue: "Client");

            migrationBuilder.AlterColumn<string>(
                name: "service_type",
                schema: "clients",
                table: "clients",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "agreement_reference",
                schema: "clients",
                table: "clients",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                schema: "clients",
                table: "clients",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Client");

            // Drop defaults after backfill
            migrationBuilder.Sql("ALTER TABLE clients.clients ALTER COLUMN type DROP DEFAULT");
            migrationBuilder.Sql("ALTER TABLE clients.rm_clients ALTER COLUMN type DROP DEFAULT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "agreement_reference",
                schema: "clients",
                table: "rm_clients");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "clients",
                table: "rm_clients");

            migrationBuilder.DropColumn(
                name: "agreement_reference",
                schema: "clients",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "type",
                schema: "clients",
                table: "clients");

            migrationBuilder.AlterColumn<string>(
                name: "service_type",
                schema: "clients",
                table: "rm_clients",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "service_type",
                schema: "clients",
                table: "clients",
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
