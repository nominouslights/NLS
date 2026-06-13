using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionItemCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FuelLevel",
                table: "trip_pre_inspections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoadAdvisories",
                table: "trip_pre_inspections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoadConditions",
                table: "trip_pre_inspections",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Temperature",
                table: "trip_pre_inspections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "trip_pre_inspections",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WeatherPulledAt",
                table: "trip_pre_inspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeatherType",
                table: "trip_pre_inspections",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllCargoDeliveredAndAccounted",
                table: "trip_post_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExteriorNoNewDamage",
                table: "trip_post_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "InteriorCleanedAndChecked",
                table: "trip_post_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "KeysReturnedAndSecured",
                table: "trip_post_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PassengersDisembarkedSafely",
                table: "trip_post_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VehicleSecuredAndPluggedIn",
                table: "trip_post_reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BoardingStatus",
                table: "trip_passengers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotBoarded");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "trip_inspection_items",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Charge",
                table: "trip_cargo_items",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHazmat",
                table: "trip_cargo_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSecured",
                table: "trip_cargo_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                table: "trip_cargo_items",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vehicle_fuel_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    FuelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FuelLitres = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalCostDollars = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    OdometerAtFuelling = table.Column<int>(type: "integer", nullable: true),
                    ReceiptPhotoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_fuel_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vehicle_fuel_entries_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_fuel_entries_FuelledAt",
                table: "vehicle_fuel_entries",
                column: "FuelledAt");

            migrationBuilder.CreateIndex(
                name: "IX_vehicle_fuel_entries_VehicleId",
                table: "vehicle_fuel_entries",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_fuel_entries");

            migrationBuilder.DropColumn(
                name: "FuelLevel",
                table: "trip_pre_inspections");

            migrationBuilder.DropColumn(
                name: "RoadAdvisories",
                table: "trip_pre_inspections");

            migrationBuilder.DropColumn(
                name: "RoadConditions",
                table: "trip_pre_inspections");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "trip_pre_inspections");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "trip_pre_inspections");

            migrationBuilder.DropColumn(
                name: "WeatherPulledAt",
                table: "trip_pre_inspections");

            migrationBuilder.DropColumn(
                name: "WeatherType",
                table: "trip_pre_inspections");

            migrationBuilder.DropColumn(
                name: "AllCargoDeliveredAndAccounted",
                table: "trip_post_reports");

            migrationBuilder.DropColumn(
                name: "ExteriorNoNewDamage",
                table: "trip_post_reports");

            migrationBuilder.DropColumn(
                name: "InteriorCleanedAndChecked",
                table: "trip_post_reports");

            migrationBuilder.DropColumn(
                name: "KeysReturnedAndSecured",
                table: "trip_post_reports");

            migrationBuilder.DropColumn(
                name: "PassengersDisembarkedSafely",
                table: "trip_post_reports");

            migrationBuilder.DropColumn(
                name: "VehicleSecuredAndPluggedIn",
                table: "trip_post_reports");

            migrationBuilder.DropColumn(
                name: "BoardingStatus",
                table: "trip_passengers");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "trip_inspection_items");

            migrationBuilder.DropColumn(
                name: "Charge",
                table: "trip_cargo_items");

            migrationBuilder.DropColumn(
                name: "IsHazmat",
                table: "trip_cargo_items");

            migrationBuilder.DropColumn(
                name: "IsSecured",
                table: "trip_cargo_items");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                table: "trip_cargo_items");
        }
    }
}
