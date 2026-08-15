using Microsoft.EntityFrameworkCore;
using NorthernLink.Trips.Application.Abstractions;

namespace NorthernLink.Trips.Infrastructure.Persistence;

/// <summary>
/// Per-tenant "SH-####" sequence over trips.shipment_number_counters: a single atomic
/// upsert-returning statement, so two concurrent callers can never draw the same number
/// (Postgres serializes the row update). Runs on the module context's tenant session
/// (RLS-visible: the caller is either a request or ambient-tenant background work), outside the
/// aggregate save's transaction — a failed save may burn a number, which is fine: shipment
/// numbers are unique, not gapless.
/// <para>
/// A line-for-line clone of <see cref="TripNumberGenerator"/>. Sharing one generic helper was
/// considered and rejected: the two differ only in a table name and a prefix, and a shared
/// abstraction over four lines of SQL would hide the one thing that matters here — that the
/// insert and the increment are a single statement.
/// </para>
/// </summary>
internal sealed class ShipmentNumberGenerator(TripsDbContext context) : IShipmentNumberGenerator
{
    public async Task<string> NextAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var values = await context.Database
            .SqlQuery<long>($"""
                INSERT INTO trips.shipment_number_counters (tenant_id, next_value)
                VALUES ({tenantId}, 1)
                ON CONFLICT (tenant_id)
                DO UPDATE SET next_value = shipment_number_counters.next_value + 1
                RETURNING next_value AS "Value"
                """)
            .ToListAsync(cancellationToken);

        return $"SH-{values[0]}";
    }
}
