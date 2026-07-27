using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Clients.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClientInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_interactions",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    participant_contact_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    follow_up_date = table.Column<DateOnly>(type: "date", nullable: true),
                    follow_up_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_interactions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rm_client_interactions",
                schema: "clients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    participant_contact_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    follow_up_date = table.Column<DateOnly>(type: "date", nullable: true),
                    follow_up_note = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rm_client_interactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_interactions_tenant_id_client_id",
                schema: "clients",
                table: "client_interactions",
                columns: new[] { "tenant_id", "client_id" });

            migrationBuilder.CreateIndex(
                name: "IX_rm_client_interactions_tenant_id_client_id",
                schema: "clients",
                table: "rm_client_interactions",
                columns: new[] { "tenant_id", "client_id" });

            // Row-level security for client_interactions (write-side). No uniqueness constraint —
            // interactions are an append-only log, so there is no partial unique index here.
            migrationBuilder.Sql("""
                ALTER TABLE clients.client_interactions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.client_interactions FORCE ROW LEVEL SECURITY;
                CREATE POLICY client_interactions_tenant_policy ON clients.client_interactions
                    USING (tenant_id = current_setting('app.tenant_id')::uuid
                        OR current_setting('app.is_system') = 'true')
                    WITH CHECK (tenant_id = current_setting('app.tenant_id')::uuid);
                """);

            // Row-level security for rm_client_interactions (read-side).
            migrationBuilder.Sql("""
                ALTER TABLE clients.rm_client_interactions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE clients.rm_client_interactions FORCE ROW LEVEL SECURITY;
                CREATE POLICY rm_client_interactions_tenant_read ON clients.rm_client_interactions
                    USING (tenant_id = current_setting('app.tenant_id')::uuid
                        OR current_setting('app.is_system') = 'true');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS client_interactions_tenant_policy ON clients.client_interactions");
            migrationBuilder.Sql("DROP POLICY IF EXISTS rm_client_interactions_tenant_read ON clients.rm_client_interactions");

            migrationBuilder.DropTable(
                name: "client_interactions",
                schema: "clients");

            migrationBuilder.DropTable(
                name: "rm_client_interactions",
                schema: "clients");
        }
    }
}
