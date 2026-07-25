using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBootstrapTokenExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-tuned from the scaffolded non-nullable-with-sentinel-default version:
            // add nullable → backfill → alter non-nullable, so existing rows get a real
            // expiry instead of a 0001-01-01 sentinel. Pre-existing tokens were issued
            // before expiry existed; created_at + 15 minutes (BootstrapTokenPolicy.Lifetime)
            // makes every one of them instantly expired — deliberate, they were minted
            // under the old never-expires rule the whole change exists to revoke.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at_utc",
                schema: "identity",
                table: "admin_bootstrap_tokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE identity.admin_bootstrap_tokens SET expires_at_utc = created_at_utc + interval '15 minutes';");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "expires_at_utc",
                schema: "identity",
                table: "admin_bootstrap_tokens",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "expires_at_utc",
                schema: "identity",
                table: "admin_bootstrap_tokens");
        }
    }
}
