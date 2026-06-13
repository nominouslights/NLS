using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengerProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "passenger_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastBookedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_passenger_profiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_passenger_profiles_ClientId_NormalizedName",
                table: "passenger_profiles",
                columns: new[] { "ClientId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_passenger_profiles_LastBookedAt",
                table: "passenger_profiles",
                column: "LastBookedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "passenger_profiles");
        }
    }
}
