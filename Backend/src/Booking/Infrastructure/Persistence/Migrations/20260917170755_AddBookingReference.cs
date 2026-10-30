using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Booking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive: a new NOT NULL column with a per-tenant unique index. EF's scaffold
            // wanted `NOT NULL DEFAULT ''`, which would stamp every existing booking with the
            // same empty reference and fail the unique index on the second row — so the
            // column is added nullable, backfilled, then tightened.
            migrationBuilder.AddColumn<string>(
                name: "reference",
                schema: "booking",
                table: "bookings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            // Backfill existing bookings with a deterministic reference derived from the row
            // id: `NL-` + the first six hex chars of md5(id), uppercased, with 0→X and 1→Y so
            // every character is in BookingReference.Alphabet (hex A–F and 2–9 already are;
            // 0 and 1 are the only look-alikes hex can produce). Uniqueness is not guaranteed
            // by construction but a collision fails LOUDLY at the CreateIndex step below —
            // never silently.
            //
            // RLS is the trap here, and getting it wrong is silent. booking.bookings carries a
            // single FORCED policy (bookings_tenant_isolation, InitialBookingSchema) with no
            // `app.is_system` arm. A migration runs with no app.tenant_id set, so the policy
            // evaluates `tenant_id = NULLIF('', '')::uuid` → NULL → false, and a blind UPDATE
            // matches zero rows while the migration reports success; only the NOT NULL alter
            // below would then catch it. Same remedy as Budgeting's
            // ClearCostCentreOnRevenueBudgetCodes: drop FORCE for the duration (the app role
            // owns the table, and an unforced policy does not bind the owner), then put it
            // back. All three statements are ONE Sql batch inside EF's migration transaction —
            // ALTER TABLE is transactional in Postgres, so a failure anywhere rolls the whole
            // thing back and the table is never left unforced. No policy is created, altered
            // or dropped.
            migrationBuilder.Sql(
                """
                ALTER TABLE booking.bookings NO FORCE ROW LEVEL SECURITY;
                UPDATE booking.bookings
                   SET reference = 'NL-' || translate(upper(substr(md5(id::text), 1, 6)), '01', 'XY')
                 WHERE reference IS NULL;
                ALTER TABLE booking.bookings FORCE ROW LEVEL SECURITY;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "reference",
                schema: "booking",
                table: "bookings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(9)",
                oldMaxLength: 9,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_tenant_id_reference",
                schema: "booking",
                table: "bookings",
                columns: new[] { "tenant_id", "reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_tenant_id_reference",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "reference",
                schema: "booking",
                table: "bookings");
        }
    }
}
