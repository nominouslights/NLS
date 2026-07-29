using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlimTripManifest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trip_manifests_tenant_id_unit",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "arrival_time",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "attestations",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "certified_at",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "departure_time",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "driver_licence_no",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "driver_name",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "driver_signature_name",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_added",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_cost_cad",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_level",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_litres",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "issues",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "licence_plate",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "no_issues",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "odometer_end_km",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "odometer_start_km",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "post_trip_items",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "pre_trip_items",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "road_advisories",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "road_conditions",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "temperature_c",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "total_km",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "unit",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "visibility",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "weather",
                schema: "trips",
                table: "trip_manifests");

            migrationBuilder.DropColumn(
                name: "arrival_time",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "attestations",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "certified_at",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "departure_time",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "driver_licence_no",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "driver_name",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "driver_signature_name",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_added",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_cost_cad",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_level",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "fuel_litres",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "issues",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "licence_plate",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "no_issues",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "odometer_end_km",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "odometer_start_km",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "post_trip_items",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "pre_trip_items",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "road_advisories",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "road_conditions",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "temperature_c",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "total_km",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "unit",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "visibility",
                schema: "trips",
                table: "rm_trip_manifests");

            migrationBuilder.DropColumn(
                name: "weather",
                schema: "trips",
                table: "rm_trip_manifests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "arrival_time",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<List<bool>>(
                name: "attestations",
                schema: "trips",
                table: "trip_manifests",
                type: "boolean[]",
                nullable: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "certified_at",
                schema: "trips",
                table: "trip_manifests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "departure_time",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_licence_no",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_name",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "driver_signature_name",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "fuel_added",
                schema: "trips",
                table: "trip_manifests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_cost_cad",
                schema: "trips",
                table: "trip_manifests",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_level",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_litres",
                schema: "trips",
                table: "trip_manifests",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "issues",
                schema: "trips",
                table: "trip_manifests",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "licence_plate",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "no_issues",
                schema: "trips",
                table: "trip_manifests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "odometer_end_km",
                schema: "trips",
                table: "trip_manifests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "odometer_start_km",
                schema: "trips",
                table: "trip_manifests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "post_trip_items",
                schema: "trips",
                table: "trip_manifests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pre_trip_items",
                schema: "trips",
                table: "trip_manifests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "road_advisories",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "road_conditions",
                schema: "trips",
                table: "trip_manifests",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "temperature_c",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_km",
                schema: "trips",
                table: "trip_manifests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                schema: "trips",
                table: "trip_manifests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "weather",
                schema: "trips",
                table: "trip_manifests",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "arrival_time",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<bool>>(
                name: "attestations",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "boolean[]",
                nullable: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "certified_at",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "departure_time",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_licence_no",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_name",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "driver_signature_name",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "fuel_added",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_cost_cad",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_level",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_litres",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "issues",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "licence_plate",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "no_issues",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "odometer_end_km",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "odometer_start_km",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "post_trip_items",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pre_trip_items",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "road_advisories",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "road_conditions",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "temperature_c",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_km",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "weather",
                schema: "trips",
                table: "rm_trip_manifests",
                type: "text[]",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_trip_manifests_tenant_id_unit",
                schema: "trips",
                table: "trip_manifests",
                columns: new[] { "tenant_id", "unit" });
        }
    }
}
