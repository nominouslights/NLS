using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RebuildTripPassengersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create staging table using _new suffix on constraints to avoid name
            // collision with the still-live trip_passengers table (PostgreSQL
            // constraint/index names are schema-scoped, not table-scoped).
            migrationBuilder.Sql(@"
                CREATE TABLE trip_passengers_new (
                    ""Id""               uuid                        NOT NULL,
                    ""TripId""           uuid                        NOT NULL,
                    ""Name""             character varying(200)      NOT NULL,
                    ""ContactInfo""      character varying(200),
                    ""SeatNumber""       integer,
                    ""PaymentStatus""    character varying(20)       NOT NULL,
                    ""BookingReference"" character varying(10),
                    ""Phone""            character varying(20),
                    ""Email""            character varying(200),
                    ""Direction""        character varying(20),
                    ""CutoffDeadline""   timestamp with time zone,
                    ""BookedAt""         timestamp with time zone    NOT NULL DEFAULT NOW(),
                    ""Fare""             numeric(10,2),
                    CONSTRAINT ""PK_trip_passengers_new"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_trip_passengers_new_trips_TripId"" FOREIGN KEY (""TripId"")
                        REFERENCES trips(""Id"") ON DELETE CASCADE
                );
            ");

            // Transfer all rows, remapping legacy enum values on the way
            migrationBuilder.Sql(@"
                INSERT INTO trip_passengers_new (
                    ""Id"", ""TripId"", ""Name"", ""ContactInfo"", ""SeatNumber"",
                    ""PaymentStatus"",
                    ""BookingReference"", ""Phone"", ""Email"",
                    ""Direction"", ""CutoffDeadline"", ""BookedAt"", ""Fare""
                )
                SELECT
                    ""Id"", ""TripId"", ""Name"", ""ContactInfo"", ""SeatNumber"",
                    CASE ""PaymentStatus""
                        WHEN 'Pending'  THEN 'Tentative'
                        WHEN 'Paid'     THEN 'Confirmed'
                        ELSE                 ""PaymentStatus""
                    END,
                    ""BookingReference"", ""Phone"", ""Email"",
                    ""Direction"", ""CutoffDeadline"", ""BookedAt"", ""Fare""
                FROM trip_passengers;
            ");

            // Drop old table, promote the staging table, then rename constraints
            // to the names EF Core expects (must happen after the DROP so the
            // original PK/FK names are free in the schema).
            migrationBuilder.Sql(@"
                DROP TABLE trip_passengers;
                ALTER TABLE trip_passengers_new RENAME TO trip_passengers;
                ALTER TABLE trip_passengers RENAME CONSTRAINT ""PK_trip_passengers_new"" TO ""PK_trip_passengers"";
                ALTER TABLE trip_passengers RENAME CONSTRAINT ""FK_trip_passengers_new_trips_TripId"" TO ""FK_trip_passengers_trips_TripId"";
            ");

            // Recreate indexes on the renamed table
            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_trip_passengers_TripId""
                    ON trip_passengers(""TripId"");

                CREATE UNIQUE INDEX ""IX_trip_passengers_BookingReference""
                    ON trip_passengers(""BookingReference"")
                    WHERE ""BookingReference"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the enum remap
            migrationBuilder.Sql(@"
                UPDATE trip_passengers SET ""PaymentStatus"" = 'Pending' WHERE ""PaymentStatus"" = 'Tentative';
                UPDATE trip_passengers SET ""PaymentStatus"" = 'Paid'    WHERE ""PaymentStatus"" = 'Confirmed';
            ");

            // Revert the index filter to its previous (incorrect) form
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
