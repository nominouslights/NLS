using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityBookingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "VehicleId",
                table: "trips",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "BookedAt",
                table: "trip_passengers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<string>(
                name: "BookingReference",
                table: "trip_passengers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CutoffDeadline",
                table: "trip_passengers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Direction",
                table: "trip_passengers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "trip_passengers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fare",
                table: "trip_passengers",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "trip_passengers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "community_calendar_blocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BlockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_calendar_blocks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers",
                column: "BookingReference",
                unique: true,
                filter: "\"BookingReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_community_calendar_blocks_BlockedDate",
                table: "community_calendar_blocks",
                column: "BlockedDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_calendar_blocks");

            migrationBuilder.DropIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "BookedAt",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "BookingReference",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "CutoffDeadline",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "Direction",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "Fare",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "trip_passengers");

            migrationBuilder.AlterColumn<Guid>(
                name: "VehicleId",
                table: "trips",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
