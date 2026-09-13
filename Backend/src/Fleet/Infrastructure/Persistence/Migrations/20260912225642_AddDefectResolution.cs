using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Fleet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDefectResolution : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Intentionally empty, and kept anyway. The defect resolution/recurrence fields are new
        /// properties on an owned record that is mapped with <c>OwnsMany(...).ToJson("defects")</c>
        /// on both fleet.vehicle_inspections and fleet.rm_vehicle_inspections, so they change the
        /// jsonb PAYLOAD shape only — there is no DDL to emit. The file exists so the model
        /// snapshot stays accurate for the next real diff; deleting it would make the following
        /// migration try to re-add everything this one recorded.
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
