using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatingProvinces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "destination_province",
                schema: "trips",
                table: "trips",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "MB");

            migrationBuilder.AddColumn<string>(
                name: "origin_province",
                schema: "trips",
                table: "trips",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "MB");

            migrationBuilder.AddColumn<string>(
                name: "destination_province",
                schema: "trips",
                table: "rm_trips",
                type: "text",
                nullable: false,
                defaultValue: "MB");

            migrationBuilder.AddColumn<string>(
                name: "origin_province",
                schema: "trips",
                table: "rm_trips",
                type: "text",
                nullable: false,
                defaultValue: "MB");

            // ---- Hand-appended: normalise catalog stop provinces to their two-letter codes ----
            // Address.Province used to be free text ("Manitoba"); from this migration on the
            // aggregate stores only the CanadianProvince code ("MB"), which is what the
            // Manitoba-only operating-area gate compares against when a stop is placed on a
            // route. Existing rows are rewritten in place so the gate never has to guess at a
            // spelling: the thirteen full English names and the codes themselves, matched
            // case-insensitively and trimmed, map to the code; anything else (a typo, a French
            // name, an abbreviation) is deliberately left untouched — the route-stop resolver
            // still normalises it on read, and a value it cannot recognise refuses the route
            // with Trips.Route.StopUnknownProvince until the stop is corrected in the catalog.
            //
            // Both the write table and its rm_stops projection are updated: the projection is
            // only re-run on a Stop event, and nothing here raises one. Columns are untouched
            // (province stays varchar(120); the new value is shorter).
            //
            // RLS: trips.stops is FORCE ROW LEVEL SECURITY with a stops_system_access policy
            // and rm_stops carries the app.is_system OR-arm, so the transaction-local
            // set_config (same transaction as the UPDATEs — EF runs the migration inside one)
            // lets one statement rewrite every tenant's rows. Without it the UPDATE silently
            // matches nothing and the migration "succeeds" having done nothing.
            // Idempotent: a second run finds the codes already in place and changes no row.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    PERFORM set_config('app.is_system', 'true', true);

                    UPDATE trips.stops s
                    SET province = m.code
                    FROM (VALUES
                        ('british columbia', 'BC'), ('bc', 'BC'),
                        ('alberta', 'AB'), ('ab', 'AB'),
                        ('saskatchewan', 'SK'), ('sk', 'SK'),
                        ('manitoba', 'MB'), ('mb', 'MB'),
                        ('ontario', 'ON'), ('on', 'ON'),
                        ('quebec', 'QC'), ('qc', 'QC'),
                        ('new brunswick', 'NB'), ('nb', 'NB'),
                        ('nova scotia', 'NS'), ('ns', 'NS'),
                        ('prince edward island', 'PE'), ('pe', 'PE'),
                        ('newfoundland and labrador', 'NL'), ('nl', 'NL'),
                        ('yukon', 'YT'), ('yt', 'YT'),
                        ('northwest territories', 'NT'), ('nt', 'NT'),
                        ('nunavut', 'NU'), ('nu', 'NU')
                    ) AS m(name, code)
                    WHERE lower(btrim(s.province)) = m.name
                      AND s.province IS DISTINCT FROM m.code;

                    UPDATE trips.rm_stops s
                    SET province = m.code
                    FROM (VALUES
                        ('british columbia', 'BC'), ('bc', 'BC'),
                        ('alberta', 'AB'), ('ab', 'AB'),
                        ('saskatchewan', 'SK'), ('sk', 'SK'),
                        ('manitoba', 'MB'), ('mb', 'MB'),
                        ('ontario', 'ON'), ('on', 'ON'),
                        ('quebec', 'QC'), ('qc', 'QC'),
                        ('new brunswick', 'NB'), ('nb', 'NB'),
                        ('nova scotia', 'NS'), ('ns', 'NS'),
                        ('prince edward island', 'PE'), ('pe', 'PE'),
                        ('newfoundland and labrador', 'NL'), ('nl', 'NL'),
                        ('yukon', 'YT'), ('yt', 'YT'),
                        ('northwest territories', 'NT'), ('nt', 'NT'),
                        ('nunavut', 'NU'), ('nu', 'NU')
                    ) AS m(name, code)
                    WHERE lower(btrim(s.province)) = m.name
                      AND s.province IS DISTINCT FROM m.code;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The province normalisation is not reversed: the codes are valid values of the
            // (unchanged) varchar column either way, and the old free-text spellings were never
            // canonical to begin with.
            migrationBuilder.DropColumn(
                name: "destination_province",
                schema: "trips",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "origin_province",
                schema: "trips",
                table: "trips");

            migrationBuilder.DropColumn(
                name: "destination_province",
                schema: "trips",
                table: "rm_trips");

            migrationBuilder.DropColumn(
                name: "origin_province",
                schema: "trips",
                table: "rm_trips");
        }
    }
}
