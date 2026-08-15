using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "client_id",
                schema: "notifications",
                table: "rm_email_dispatches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "client_id",
                schema: "notifications",
                table: "email_dispatches",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "client_id",
                schema: "notifications",
                table: "rm_email_dispatches");

            migrationBuilder.DropColumn(
                name: "client_id",
                schema: "notifications",
                table: "email_dispatches");
        }
    }
}
