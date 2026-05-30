using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MigratePassengerPaymentStatusValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename legacy enum values written before the 5-state status model
            migrationBuilder.Sql(@"
                UPDATE trip_passengers SET ""PaymentStatus"" = 'Tentative' WHERE ""PaymentStatus"" = 'Pending';
                UPDATE trip_passengers SET ""PaymentStatus"" = 'Confirmed' WHERE ""PaymentStatus"" = 'Paid';
            ");

            migrationBuilder.DropIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers",
                column: "BookingReference",
                unique: true,
                filter: "\"BookingReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers");

            migrationBuilder.CreateIndex(
                name: "IX_trip_passengers_BookingReference",
                table: "trip_passengers",
                column: "BookingReference",
                unique: true,
                filter: "booking_reference IS NOT NULL");
        }
    }
}
