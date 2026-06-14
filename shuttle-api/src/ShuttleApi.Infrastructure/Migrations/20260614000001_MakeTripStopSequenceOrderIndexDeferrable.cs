using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeTripStopSequenceOrderIndexDeferrable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Recreate the unique index as DEFERRABLE INITIALLY DEFERRED so EF Core can renumber
            // existing stop sequence orders within a single SaveChanges transaction without
            // hitting a transient uniqueness violation between individual UPDATE statements.
            migrationBuilder.DropIndex(
                name: "IX_trip_stops_TripId_SequenceOrder",
                table: "trip_stops");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_trip_stops_TripId_SequenceOrder"
                ON trip_stops ("TripId", "SequenceOrder")
                DEFERRABLE INITIALLY DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trip_stops_TripId_SequenceOrder",
                table: "trip_stops");

            migrationBuilder.CreateIndex(
                name: "IX_trip_stops_TripId_SequenceOrder",
                table: "trip_stops",
                columns: new[] { "TripId", "SequenceOrder" },
                unique: true);
        }
    }
}
