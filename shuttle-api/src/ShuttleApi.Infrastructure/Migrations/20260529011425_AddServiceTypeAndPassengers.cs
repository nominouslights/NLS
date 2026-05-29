using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTypeAndPassengers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                table: "trips",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<decimal>(
                name: "PricePerSeat",
                table: "trips",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeatCapacity",
                table: "trips",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceType",
                table: "trips",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Charter");

            migrationBuilder.CreateTable(
                name: "trip_passengers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactInfo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SeatNumber = table.Column<int>(type: "integer", nullable: true),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_passengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_passengers_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trips_ServiceType",
                table: "trips",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_TripId",
                table: "trip_passengers",
                column: "TripId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_passengers");

            migrationBuilder.DropIndex(
                name: "IX_trips_ServiceType",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "PricePerSeat",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "SeatCapacity",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "ServiceType",
                table: "trips");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
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
