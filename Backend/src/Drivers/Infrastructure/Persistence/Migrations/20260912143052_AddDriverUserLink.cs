using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Drivers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverUserLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "drivers",
                table: "rm_drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "user_id",
                schema: "drivers",
                table: "drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rm_drivers_user_id",
                schema: "drivers",
                table: "rm_drivers",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_drivers_user_id",
                schema: "drivers",
                table: "drivers",
                column: "user_id",
                unique: true,
                filter: "user_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rm_drivers_user_id",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropIndex(
                name: "IX_drivers_user_id",
                schema: "drivers",
                table: "drivers");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "drivers",
                table: "rm_drivers");

            migrationBuilder.DropColumn(
                name: "user_id",
                schema: "drivers",
                table: "drivers");
        }
    }
}
