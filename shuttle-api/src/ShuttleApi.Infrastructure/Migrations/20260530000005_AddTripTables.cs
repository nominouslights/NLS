using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260530000005_AddTripTables")]
    public partial class AddTripTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ServiceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurchaseOrderNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VehicleType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SeatCapacity = table.Column<int>(type: "integer", nullable: true),
                    PricePerSeat = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trip_stops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    LocationName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_stops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_stops_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_passengers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContactInfo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SeatNumber = table.Column<int>(type: "integer", nullable: true),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BookingReference = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CutoffDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BookedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Fare = table.Column<decimal>(type: "numeric(10,2)", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "trip_pre_inspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    OdometerStart = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_pre_inspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_pre_inspections_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_post_reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TripId = table.Column<Guid>(type: "uuid", nullable: false),
                    OdometerStart = table.Column<int>(type: "integer", nullable: false),
                    OdometerEnd = table.Column<int>(type: "integer", nullable: false),
                    FuelAddedLitres = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    FuelCostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    HasIncident = table.Column<bool>(type: "boolean", nullable: false),
                    IncidentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    IncidentDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AdditionalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsReadyToInvoice = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_post_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_post_reports_trips_TripId",
                        column: x => x.TripId,
                        principalTable: "trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_inspection_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PreInspectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_inspection_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_inspection_items_trip_pre_inspections_PreInspectionId",
                        column: x => x.PreInspectionId,
                        principalTable: "trip_pre_inspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trips_ClientId",
                table: "trips",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_trips_DriverId_Status",
                table: "trips",
                columns: new[] { "DriverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_trips_ServiceType",
                table: "trips",
                column: "ServiceType");

            migrationBuilder.CreateIndex(
                name: "IX_trips_Status",
                table: "trips",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_trip_stops_TripId_SequenceOrder",
                table: "trip_stops",
                columns: new[] { "TripId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_TripId",
                table: "trip_passengers",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers",
                column: "BookingReference",
                unique: true,
                filter: "\"BookingReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_trip_pre_inspections_TripId",
                table: "trip_pre_inspections",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_inspection_items_PreInspectionId",
                table: "trip_inspection_items",
                column: "PreInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_trip_post_reports_TripId",
                table: "trip_post_reports",
                column: "TripId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "trip_inspection_items");
            migrationBuilder.DropTable(name: "trip_passengers");
            migrationBuilder.DropTable(name: "trip_post_reports");
            migrationBuilder.DropTable(name: "trip_pre_inspections");
            migrationBuilder.DropTable(name: "trip_stops");
            migrationBuilder.DropTable(name: "trips");
        }
    }
}
