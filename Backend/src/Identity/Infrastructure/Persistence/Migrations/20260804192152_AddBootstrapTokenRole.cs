using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBootstrapTokenRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-tuned from the scaffolded non-nullable-with-empty-string-default version, same
            // shape as AddBootstrapTokenExpiry: add nullable → backfill → alter non-nullable, so
            // existing rows get a real role rather than "" (which User.Create would then reject at
            // redemption, stranding the token holder). Pre-existing tokens were minted under the
            // always-Admin rule, and Admin always meant Owner in practice — see
            // RenameAdminRoleToOwner, which applies the same mapping to identity.users.
            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "identity",
                table: "admin_bootstrap_tokens",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(
                """
                -- identity.admin_bootstrap_tokens has FORCE ROW LEVEL SECURITY
                -- (InitialIdentitySchema.cs:232-236). A migration connection sets neither
                -- app.tenant_id nor app.is_system, and FORCE binds the table owner too, so without
                -- this the UPDATE matches zero rows and still reports success — leaving NULLs that
                -- the ALTER below would then fail on. is_local=true scopes the setting to this
                -- migration's transaction.
                SELECT set_config('app.is_system', 'true', true);
                UPDATE identity.admin_bootstrap_tokens SET role = 'Owner' WHERE role IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "role",
                schema: "identity",
                table: "admin_bootstrap_tokens",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                schema: "identity",
                table: "admin_bootstrap_tokens");
        }
    }
}
