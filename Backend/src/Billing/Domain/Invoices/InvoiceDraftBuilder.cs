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
/// by <c>RoundTripKey</c>: a key with both legs prices as one line (qty 1 × rate per round
/// trip); a lone leg prices at qty 0.5 with its description flagged for review; trips with
/// no key (ad-hoc/charter/cargo) are left unclaimed for manual lines on the draft. GST is
/// not a line — the invoice computes it from its snapshot rate when applicable.
/// </summary>
public static class InvoiceDraftBuilder
{
    public const string UnpairedLegFlag = "½ round trip — unpaired leg, review";

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
            var first = legs[0];
            var serviceDate = legs.Min(t => t.ServiceDate);
            var isPair = legs.Count >= 2;

            var lineResult = InvoiceLine.Create(
                isPair
                    ? $"Corridor round trip · {first.RouteName} · {serviceDate:yyyy-MM-dd}"
                    : $"{UnpairedLegFlag} · {first.RouteName} · {serviceDate:yyyy-MM-dd}",
                legs.Select(t => t.Id).ToList(),
                isPair ? null : first.TripNumber,
                serviceDate,
                quantity: isPair ? 1m : 0.5m,
                unitPriceCad: rate);

            if (lineResult.IsFailure)
            {
                return Result.Failure<InvoiceDraft>(lineResult.Error);
            }

            lines.Add(lineResult.Value);
            claimed.AddRange(legs.Select(t => t.Id));
        }

        return Result.Success(new InvoiceDraft(contract, lines, claimed));
    }
}
