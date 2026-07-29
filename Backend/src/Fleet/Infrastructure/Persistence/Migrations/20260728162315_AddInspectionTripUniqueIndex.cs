using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Partial unique index enforcing one pre-trip and one post-trip inspection per trip — the
    /// DB backstop for the enter-path guard. Partial (WHERE trip_number IS NOT NULL) so standalone
    /// vehicle entries with no trip context are unconstrained. Index only: no RLS block — the
    /// table already carries its tenant policy from an earlier migration and nothing is created.
    /// </summary>
    public partial class AddInspectionTripUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_vehicle_inspections_tenant_id_trip_number_type",
                schema: "fleet",
                table: "vehicle_inspections",
                columns: new[] { "tenant_id", "trip_number", "type" },
                unique: true,
                filter: "trip_number IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_vehicle_inspections_tenant_id_trip_number_type",
                schema: "fleet",
                table: "vehicle_inspections");
        }
    }
}
