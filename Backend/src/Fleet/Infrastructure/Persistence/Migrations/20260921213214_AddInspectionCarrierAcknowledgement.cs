using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionCarrierAcknowledgement : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Additive and non-breaking: four nullable columns with no defaults, on both
        /// fleet.vehicle_inspections and its projection table fleet.rm_vehicle_inspections. Every
        /// existing row reads back as unacknowledged and uncertified-by-statement, which is the
        /// truth about them.
        ///
        /// The other half of NL-PTI-01 — the checklist tri-state and per-item note — emits no DDL
        /// at all: those are new properties on an owned record mapped with
        /// <c>OwnsMany(...).ToJson("checklist_items")</c> on both tables, so they change the jsonb
        /// PAYLOAD only. Same precedent as AddDefectResolution.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "carrier_acknowledged_at_utc",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_acknowledged_by",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_acknowledgement_note",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certification_statement",
                schema: "fleet",
                table: "vehicle_inspections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "carrier_acknowledged_at_utc",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_acknowledged_by",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "carrier_acknowledgement_note",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "certification_statement",
                schema: "fleet",
                table: "rm_vehicle_inspections",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carrier_acknowledged_at_utc",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "carrier_acknowledged_by",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "carrier_acknowledgement_note",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "certification_statement",
                schema: "fleet",
                table: "vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "carrier_acknowledged_at_utc",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "carrier_acknowledged_by",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "carrier_acknowledgement_note",
                schema: "fleet",
                table: "rm_vehicle_inspections");

            migrationBuilder.DropColumn(
                name: "certification_statement",
                schema: "fleet",
                table: "rm_vehicle_inspections");
        }
    }
}
