using NorthernLink.Drivers.Application.Hos.Ledger;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>
/// Loads everything the rule engine and the compliance reads need beside the duty days:
/// exceptions, out-of-service declarations, ELD malfunction reports, findings, and the driver's
/// prior-records attestation. Write-side on purpose, like <see cref="IDutyDayRepository"/>: a
/// declaration recorded a second ago must already block or extend the figures.
/// </summary>
public interface IComplianceRecordsReader
{
    /// <summary>
    /// The driver's records that touch [<paramref name="fromUtc"/>, ∞) — plus findings dated from
    /// that instant's day on — or null when the driver does not exist for the current tenant.
    /// </summary>
    Task<ComplianceRecords?> LoadAsync(Guid driverId, DateTimeOffset fromUtc, CancellationToken cancellationToken = default);

    /// <summary>The same for every driver in the tenant, keyed by driver id (drivers with nothing on file are absent).</summary>
    Task<IReadOnlyDictionary<Guid, ComplianceRecords>> LoadForTenantAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken = default);
}
