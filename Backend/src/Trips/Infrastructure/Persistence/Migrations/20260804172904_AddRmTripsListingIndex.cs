using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRmTripsListingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_rm_trips_tenant_id_service_date_window_start_trip_number",
                schema: "trips",
                table: "rm_trips",
                columns: new[] { "tenant_id", "service_date", "window_start", "trip_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_rm_trips_tenant_id_service_date_window_start_trip_number",
                schema: "trips",
                table: "rm_trips");
        }
    }
}
