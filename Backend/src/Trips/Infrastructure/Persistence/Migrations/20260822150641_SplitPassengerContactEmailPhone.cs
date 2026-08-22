using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NorthernLink.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitPassengerContactEmailPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The ManifestPassenger jsonb value object split its single free-text `Contact`
            // field into `Email` and `Phone`. That property rename is a no-op for the physical
            // `passengers jsonb` column (EF only records it in the model snapshot), so the
            // backfill below is the whole substance of this migration: it rewrites each
            // passenger element on both the aggregate table and the read-model projection,
            // classifying the old `Contact` value as an email (when it matches an email regex)
            // or a phone (any other non-empty value), then dropping the `Contact` key.
            //
            // RLS: both trips.trip_manifests and trips.rm_trip_manifests carry
            // FORCE ROW LEVEL SECURITY with an app.is_system policy. set_config(..., true)
            // is transaction-local (EF runs a migration in one transaction) and MUST sit in
            // the SAME migrationBuilder.Sql batch as the UPDATE so the cross-tenant rewrite
            // passes RLS instead of silently touching zero rows.
            //
            // jsonb_build_object with a NULL value keeps the key present with a JSON null,
            // which reads back as a null Email/Phone — acceptable and matches the nullable
            // record fields. Guarded to array-valued, non-empty passengers so empty/absent
            // manifests are untouched; re-running is a harmless no-op once Contact is gone.
            migrationBuilder.Sql(
                """
                SELECT set_config('app.is_system', 'true', true);

                UPDATE trips.trip_manifests
                SET passengers = (
                    SELECT jsonb_agg(
                        (elem - 'Contact') || jsonb_build_object(
                            'Email', CASE WHEN elem->>'Contact' ~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$' THEN elem->>'Contact' END,
                            'Phone', CASE WHEN elem->>'Contact' IS NOT NULL AND elem->>'Contact' !~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$' THEN elem->>'Contact' END
                        )
                    )
                    FROM jsonb_array_elements(passengers) AS elem
                )
                WHERE passengers IS NOT NULL AND jsonb_typeof(passengers) = 'array' AND jsonb_array_length(passengers) > 0;

                UPDATE trips.rm_trip_manifests
                SET passengers = (
                    SELECT jsonb_agg(
                        (elem - 'Contact') || jsonb_build_object(
                            'Email', CASE WHEN elem->>'Contact' ~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$' THEN elem->>'Contact' END,
                            'Phone', CASE WHEN elem->>'Contact' IS NOT NULL AND elem->>'Contact' !~ '^[^[:space:]@]+@[^[:space:]@]+\.[^[:space:]@]+$' THEN elem->>'Contact' END
                        )
                    )
                    FROM jsonb_array_elements(passengers) AS elem
                )
                WHERE passengers IS NOT NULL AND jsonb_typeof(passengers) = 'array' AND jsonb_array_length(passengers) > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort inverse. The forward split is lossy to reverse: once Email and Phone
            // are separate keys we can't know which single value the original Contact held if a
            // passenger somehow carried both. This coalesces Email first, then Phone, back into
            // a single Contact key and drops the split keys — good enough to run the aggregate
            // and read model against the pre-split shape, but not a guaranteed round-trip.
            //
            // Same RLS handling as Up(): transaction-local system flag in the same batch.
            migrationBuilder.Sql(
                """
                SELECT set_config('app.is_system', 'true', true);

                UPDATE trips.trip_manifests
                SET passengers = (
                    SELECT jsonb_agg(
                        (elem - 'Email' - 'Phone') || jsonb_build_object(
                            'Contact', COALESCE(NULLIF(elem->>'Email', ''), NULLIF(elem->>'Phone', ''))
                        )
                    )
                    FROM jsonb_array_elements(passengers) AS elem
                )
                WHERE passengers IS NOT NULL AND jsonb_typeof(passengers) = 'array' AND jsonb_array_length(passengers) > 0;

                UPDATE trips.rm_trip_manifests
                SET passengers = (
                    SELECT jsonb_agg(
                        (elem - 'Email' - 'Phone') || jsonb_build_object(
                            'Contact', COALESCE(NULLIF(elem->>'Email', ''), NULLIF(elem->>'Phone', ''))
                        )
                    )
                    FROM jsonb_array_elements(passengers) AS elem
                )
                WHERE passengers IS NOT NULL AND jsonb_typeof(passengers) = 'array' AND jsonb_array_length(passengers) > 0;
                """);
        }
    }
}
