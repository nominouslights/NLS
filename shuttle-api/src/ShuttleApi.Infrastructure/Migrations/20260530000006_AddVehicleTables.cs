using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260530000006_AddVehicleTables")]
    public partial class AddVehicleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VIN = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    Make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LicensePlate = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Province = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    VehicleType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PassengerCapacity = table.Column<int>(type: "integer", nullable: false),
                    CurrentOdometerKm = table.Column<int>(type: "integer", nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistrationExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InsuranceProvider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InsurancePolicyNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InsuranceExpiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StatusNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_service_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FluidType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsPlanned = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OdometerAtService = table.Column<int>(type: "integer", nullable: true),
                    EstimatedCostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ActualCostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ServiceProvider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TechnicianName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PartsNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsWarrantyWork = table.Column<bool>(type: "boolean", nullable: false),
                    NextServiceDueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextServiceDueOdometerKm = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_service_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_service_records_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vehicle_inspection_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    InspectionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    InspectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InspectorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InspectionFacility = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CertificateNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InspectionResult = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DeficienciesNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrectiveActionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_inspection_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_inspection_records_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_UnitCode",
                table: "vehicles",
                column: "UnitCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_VIN",
                table: "vehicles",
                column: "VIN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_LicensePlate",
                table: "vehicles",
                column: "LicensePlate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_service_records_VehicleId",
                table: "vehicle_service_records",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_service_records_VehicleId_ServiceCategory",
                table: "vehicle_service_records",
                columns: new[] { "VehicleId", "ServiceCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_inspection_records_VehicleId",
                table: "vehicle_inspection_records",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_inspection_records_VehicleId_InspectionType",
                table: "vehicle_inspection_records",
                columns: new[] { "VehicleId", "InspectionType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "vehicle_inspection_records");
            migrationBuilder.DropTable(name: "vehicle_service_records");
            migrationBuilder.DropTable(name: "vehicles");
        }
    }
}
