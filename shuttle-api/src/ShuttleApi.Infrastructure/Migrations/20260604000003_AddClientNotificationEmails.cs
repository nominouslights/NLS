using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShuttleApi.Infrastructure.Persistence;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260604000003_AddClientNotificationEmails")]
    public partial class AddClientNotificationEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_notification_emails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_notification_emails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_notification_emails_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_notification_emails_ClientId",
                table: "client_notification_emails",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_notification_emails_ClientId_Category_Email",
                table: "client_notification_emails",
                columns: new[] { "ClientId", "Category", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "client_notification_emails");
        }
    }
}
