using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionTripSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<bool>>(
                name: "attestations",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "boolean[]",
                nullable: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "certified_at",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_signature_name",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fuel_added",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_cost_cad",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_level",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_litres",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "numeric(8,2)",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "issues",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "road_advisories",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "road_conditions",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "temperature_c",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vehicle_id",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "weather",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<List<bool>>(
                name: "attestations",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "boolean[]",
                nullable: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "certified_at",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_signature_name",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "fuel_added",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_cost_cad",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fuel_level",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fuel_litres",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "issues",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text[]",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "road_advisories",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "road_conditions",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "temperature_c",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vehicle_id",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "visibility",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "weather",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            // InspectionSource.PaperTranscription was renamed to Dispatcher (source persists as
            // its enum name). Convert any existing rows so they still materialize into the enum.
            // No RLS block here: these are ALTERs on tables that already carry the tenant policy
            // from their original migrations — new columns inherit it — and no table is created.
            migrationBuilder.Sql(
                """
                UPDATE fleet.vehicle_inspections SET source = 'Dispatcher' WHERE source = 'PaperTranscription';
                UPDATE fleet.rm_vehicle_inspections SET source = 'Dispatcher' WHERE source = 'PaperTranscription';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attestations",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "certified_at",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "driver_signature_name",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_added",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_cost_cad",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_level",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_litres",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "issues",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "road_advisories",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "road_conditions",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "temperature_c",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "vehicle_id",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "visibility",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "weather",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "attestations",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "certified_at",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "driver_signature_name",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_added",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_cost_cad",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_level",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "fuel_litres",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "issues",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "road_advisories",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "road_conditions",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "temperature_c",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "vehicle_id",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "visibility",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "weather",
                schema: "fleet",
                table: "rm_vehicle_inspections");
        }
    }
}
