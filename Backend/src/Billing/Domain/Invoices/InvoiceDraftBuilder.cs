using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Billing.Domain.Contracts;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// The outcome of pricing a billing period: the contract the rates came from, the built
/// lines, and exactly which billable trips those lines claim. The handler stamps
/// <see cref="ClaimedTripIds"/> onto the replica rows in the same transaction that saves
/// the invoice.
/// </summary>
public sealed record InvoiceDraft(
    ContractSnapshot Contract,
    IReadOnlyList<InvoiceLine> Lines,
    IReadOnlyList<Guid> ClaimedTripIds);

/// <summary>
/// Pure draft-invoice pricing — no I/O, fully unit-testable. Resolves the contract
/// covering the period, filters to completed, uninvoiced trips inside it, and groups legs
/// by <c>RoundTripKey</c>. Pairing is direction-aware: a group prices as one round-trip
/// line (qty 1 × rate per round trip) only when it holds at least one <c>Outbound</c> leg
/// and at least one <c>Inbound</c> leg. Any other group — a lone leg, legs with a
/// missing direction, or two legs sharing the same direction — bills each present leg at
/// qty 0.5 on its own line, flagged for review. A pair whose legs include a deadhead
/// (empty repositioning) leg still prices at the full round-trip rate, but the line is
/// flagged so the dispatcher can apply an optional manual discount — worksheet lines are
/// editable. Trips with no key (ad-hoc/charter/cargo) are left unclaimed for manual
/// lines on the draft. GST is not a line — the invoice computes it from its snapshot
/// rate when applicable.
/// </summary>
public static class InvoiceDraftBuilder
{
    public const string UnpairedLegFlag = "½ round trip — unpaired leg, review";

    public const string DeadheadReturnFlag = "round trip incl. deadhead return — discount optional";

    public static Result<InvoiceDraft> Build(
        IReadOnlyList<ContractSnapshot> clientContracts,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<BillableTrip> billableTrips)
    {
        if (periodEnd < periodStart)
        {
            return Result.Failure<InvoiceDraft>(InvoiceErrors.InvalidPeriod);
        }

        // The contract "active in the period" is the one whose own term overlaps it —
        // status is irrelevant here (a naturally Ended contract still bills its past
        // periods). Latest start wins if a renewal boundary falls mid-period.
        var contract = clientContracts
            .Where(c => c.Covers(periodStart, periodEnd))
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefault();

        if (contract is null)
        {
            return Result.Failure<InvoiceDraft>(InvoiceErrors.NoActiveContract);
        }

        if (contract.BillingModel != ContractSnapshot.RoundTripRateBillingModel
            || contract.RatePerRoundTripCad is not { } rate)
        {
            return Result.Failure<InvoiceDraft>(InvoiceErrors.NotRoundTripBilled);
        }

        // Defence in depth: the repository already scopes to client + period + uninvoiced,
        // but the builder re-filters so its own contract is airtight under test.
        var eligible = billableTrips
            .Where(t => t.IsUninvoiced
                && t.RoundTripKey is not null
                && t.ServiceDate >= periodStart
                && t.ServiceDate <= periodEnd)
            .ToList();

        var lines = new List<InvoiceLine>();
        var claimed = new List<Guid>();

        var groups = eligible
            .GroupBy(t => t.RoundTripKey!)
            .OrderBy(g => g.Min(t => t.ServiceDate))
            .ThenBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var legs = group.OrderBy(t => t.CompletedAtUtc).ToList();
            var serviceDate = legs.Min(t => t.ServiceDate);

            // A group is a complete round trip only when it pairs an Outbound leg with an
            // Inbound one — never merely on leg count. Same-direction or direction-less
            // legs are not a valid pair and each bills as an unpaired half.
            var hasOutbound = legs.Any(t => IsDirection(t, "Outbound"));
            var hasInbound = legs.Any(t => IsDirection(t, "Inbound"));

            if (hasOutbound && hasInbound)
            {
                var first = legs[0];
                var description = legs.Any(t => t.IsEmptyLeg)
                    ? $"{DeadheadReturnFlag} · {first.RouteName} · {serviceDate:yyyy-MM-dd}"
                    : $"Corridor round trip · {first.RouteName} · {serviceDate:yyyy-MM-dd}";
                var lineResult = InvoiceLine.Create(
                    description,
                    legs.Select(t => t.Id).ToList(),
                    null,
                    serviceDate,
                    quantity: 1m,
                    unitPriceCad: rate);

                if (lineResult.IsFailure)
                {
                    return Result.Failure<InvoiceDraft>(lineResult.Error);
                }

                lines.Add(lineResult.Value);
                claimed.AddRange(legs.Select(t => t.Id));
            }
            else
            {
                // Lone leg, missing direction, or duplicated direction: each present leg
                // prices at qty 0.5 on its own review-flagged line.
                foreach (var leg in legs)
                {
                    var lineResult = InvoiceLine.Create(
                        $"{UnpairedLegFlag} · {leg.RouteName} · {leg.ServiceDate:yyyy-MM-dd}",
                        [leg.Id],
                        leg.TripNumber,
                        leg.ServiceDate,
                        quantity: 0.5m,
                        unitPriceCad: rate);

                    if (lineResult.IsFailure)
                    {
                        return Result.Failure<InvoiceDraft>(lineResult.Error);
                    }

                    lines.Add(lineResult.Value);
                    claimed.Add(leg.Id);
                }
            }
        }

        return Result.Success(new InvoiceDraft(contract, lines, claimed));
    }

    private static bool IsDirection(BillableTrip trip, string direction) =>
        string.Equals(trip.Direction, direction, StringComparison.OrdinalIgnoreCase);
}
