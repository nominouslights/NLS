using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.Manifests;
using NorthernLink.Trips.Domain.Riders;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Application.Riders;

/// <summary>
/// The single upsert brain for the rider directory: folds one manifest's passengers into
/// Rider aggregates under the trip's service type. Cargo/Grocery trips carry no riders and
/// are skipped whole. Per passenger: blank names and in-manifest duplicates (same
/// normalized name) are skipped, then the (service type, normalized name) key either
/// creates a new rider or records the trip on the existing one — both idempotent, so the
/// at-least-once reaction pipeline converges. One save at the end.
/// </summary>
public sealed class ManifestRiderUpserter(IRiderRepository riders)
{
    public async Task<Result> UpsertAsync(
        TripManifest manifest,
        TripServiceType serviceType,
        CancellationToken cancellationToken)
    {
        if (serviceType is TripServiceType.Cargo or TripServiceType.Grocery)
        {
            return Result.Success();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var passenger in manifest.Passengers)
        {
            if (string.IsNullOrWhiteSpace(passenger.Name))
            {
                continue;
            }

            var normalizedName = Rider.NormalizeName(passenger.Name);
            if (normalizedName.Length == 0 || !seen.Add(normalizedName))
            {
                continue;
            }

            var contact = ContactFor(passenger);
            var existing = await riders.GetByKeyAsync(serviceType, normalizedName, cancellationToken);
            if (existing is null)
            {
                var created = Rider.Create(
                    manifest.TenantId,
                    passenger.Name,
                    serviceType,
                    contact,
                    manifest.TripDate,
                    manifest.TripNumber);
                if (created.IsFailure)
                {
                    // Defensive — blanks are filtered above, and Create has no other failure.
                    continue;
                }

                riders.Add(created.Value);
            }
            else
            {
                existing.RecordTrip(passenger.Name, contact, manifest.TripDate, manifest.TripNumber);
            }
        }

        await riders.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// The rider's single contact string from the passenger's split email/phone fields —
    /// email first (it's what pickup notifications run on), phone as the fallback.
    /// </summary>
    private static string? ContactFor(ManifestPassenger passenger) =>
        !string.IsNullOrWhiteSpace(passenger.Email) ? passenger.Email.Trim()
        : !string.IsNullOrWhiteSpace(passenger.Phone) ? passenger.Phone.Trim()
        : null;
}
