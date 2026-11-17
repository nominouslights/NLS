using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDefectResolvedWithInspection : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Intentionally empty, like <c>AddDefectResolution</c>. <c>InspectionDefect.ResolvedWithInspectionId</c>
        /// (duplicate-clearance provenance) is a new property on the owned record mapped with
        /// <c>OwnsMany(...).ToJson("defects")</c> on both fleet.vehicle_inspections and
        /// fleet.rm_vehicle_inspections, so it changes the jsonb PAYLOAD only — rows written
        /// before it read back as null, no DDL and no data migration. The file exists so the model
        /// snapshot stays accurate for the next real diff.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
