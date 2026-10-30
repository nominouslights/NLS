using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderDefectLines : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Additive and non-breaking: one <c>defects jsonb NOT NULL DEFAULT '[]'</c> column on
        /// fleet.work_orders and on its projection table fleet.rm_work_orders — the per-defect
        /// link from a work order to the inspection defects it was raised against. The default
        /// backfills every existing work order as "no defect lines", which is exactly the legacy
        /// completion path, so no data migration is needed.
        ///
        /// Hand-edited from the generated nullable column: EF cannot mark an owned collection
        /// mapped with <c>OwnsMany(...).ToJson("defects")</c> as required, so the NOT NULL and
        /// the default live here (see WorkOrderDefectLineMapping). EF always writes an empty
        /// collection as '[]', never NULL.
        ///
        /// No RLS change: both tables already carry ENABLE + FORCE ROW LEVEL SECURITY and a
        /// tenant_isolation policy (AddMaintenanceRecords, ReplaceFleetMatviewsWithProjectionTables),
        /// and RLS is row-level, so the new column is covered.
        ///
        /// The inspection side (InspectionDefect.WorkOrderId, DefectResolutionReason.NoFaultFound)
        /// emits no DDL — it changes the vehicle_inspections defects jsonb PAYLOAD only, the same
        /// precedent as AddDefectResolution.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "defects",
                schema: "fleet",
                table: "work_orders",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "defects",
                schema: "fleet",
                table: "rm_work_orders",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "defects",
                schema: "fleet",
                table: "work_orders");

            migrationBuilder.DropColumn(
                name: "defects",
                schema: "fleet",
                table: "rm_work_orders");
        }
    }
}
