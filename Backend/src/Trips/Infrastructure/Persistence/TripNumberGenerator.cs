using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Per-tenant "TR-####" sequence over trips.trip_number_counters: a single atomic
/// upsert-returning statement, so two concurrent callers can never draw the same
/// number (Postgres serializes the row update). Runs on the module context's tenant
/// session (RLS-visible: the caller is either a request or ambient-tenant background
/// work), outside the aggregate save's transaction — a failed save may burn a number,
/// which is fine: trip numbers are unique, not gapless.
/// </summary>
internal sealed class TripNumberGenerator(TripsDbContext context) : ITripNumberGenerator
{
    public async Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var values = await context.Database
            .SqlQuery<long>($"""
                INSERT INTO trips.trip_number_counters (tenant_id, next_value)
                VALUES ({tenantId}, 1)
                ON CONFLICT (tenant_id)
                DO UPDATE SET next_value = trip_number_counters.next_value + 1
                RETURNING next_value AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return $"TR-{values[0]}";
    }
}
