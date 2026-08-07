using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Identity.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data-only migration: every user predating the role model carries the literal "Admin",
    /// because both creation paths hardcoded it. That always meant Owner in practice — the first
    /// admin is the business owner standing up the platform, and bootstrap invites were minted by
    /// and for the same person. Mapping is therefore one-to-one and lossless.
    ///
    /// Renaming rather than permanently aliasing "Admin" keeps every future RequireRole list to a
    /// single value. The AdminOnly policy still accepts Roles.LegacyAdmin for one release, which
    /// covers the only gap this leaves: an access token minted just before this ran, valid for at
    /// most its 15-minute lifetime.
    /// </summary>
    public partial class RenameAdminRoleToOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- identity.users has FORCE ROW LEVEL SECURITY (InitialIdentitySchema.cs:226-230).
                -- A migration connection sets neither app.tenant_id nor app.is_system, and FORCE
                -- binds the table owner too, so without this the UPDATE below matches zero rows
                -- and reports success — the migration would appear to work while changing nothing.
                -- is_local=true scopes the setting to this migration's transaction.
                SELECT set_config('app.is_system', 'true', true);
                UPDATE identity.users SET role = 'Owner' WHERE role = 'Admin';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy in principle — an Owner created after this migration is indistinguishable from
            // a renamed one and gets folded back into "Admin". Acceptable: Down exists to unwind a
            // bad deploy, and "Admin" was the only role that existed on the other side of it.
            migrationBuilder.Sql(
                """
                SELECT set_config('app.is_system', 'true', true);
                UPDATE identity.users SET role = 'Admin' WHERE role = 'Owner';
                """);
        }
    }
}
