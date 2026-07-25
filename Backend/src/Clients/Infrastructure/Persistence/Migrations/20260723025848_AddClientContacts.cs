using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Clients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_contacts",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_contacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_client_contacts",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_client_contacts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_tenant_id_client_id",
                schema: "clients",
                table: "client_contacts",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_client_contacts_tenant_id_client_id",
                schema: "clients",
                table: "rm_client_contacts",
                columns: new[] { "tenant_id", "client_id" });

            // Partial unique index: exactly one primary contact per client.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX ix_client_contacts_one_primary_per_client
                ON clients.client_contacts (client_id) WHERE is_primary
                """);

            // Row-level security for client_contacts (write-side).
            migrationBuilder.Sql("""
                ALTER TABLE clients.client_contacts ENABLE ROW LEVEL SECURITY;
                CREATE POLICY client_contacts_tenant_policy ON clients.client_contacts
                    USING (tenant_id = current_setting('app.tenant_id')::uuid
                        OR current_setting('app.is_system') = 'true')
                    WITH CHECK (tenant_id = current_setting('app.tenant_id')::uuid);
                """);

            // Row-level security for rm_client_contacts (read-side).
            migrationBuilder.Sql("""
                ALTER TABLE clients.rm_client_contacts ENABLE ROW LEVEL SECURITY;
                CREATE POLICY rm_client_contacts_tenant_read ON clients.rm_client_contacts
                    USING (tenant_id = current_setting('app.tenant_id')::uuid
                        OR current_setting('app.is_system') = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS client_contacts_tenant_policy ON clients.client_contacts");
            migrationBuilder.Sql("DROP POLICY IF EXISTS rm_client_contacts_tenant_read ON clients.rm_client_contacts");
            migrationBuilder.Sql("DROP INDEX IF EXISTS clients.ix_client_contacts_one_primary_per_client");

            migrationBuilder.DropTable(
                name: "client_contacts",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "rm_client_contacts",
                schema: "clients");
        }
    }
}
