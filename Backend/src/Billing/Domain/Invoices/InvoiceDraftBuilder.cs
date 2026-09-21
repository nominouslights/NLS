using System.Globalization;
using NorthernLink.Billing.Domain.BillableTrips;
using NorthernLink.Billing.Domain.Contracts;
using NorthernLink.Billing.Domain.PurchaseOrders;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Billing.Domain.Invoices;

/// <summary>
/// The outcome of pricing one purchase order's share of a billing period: the contract the
/// fallback rate came from, the PO this worksheet covers, the built lines, exactly which
/// billable trips those lines claim, and any draft-level warnings. One draft becomes one
/// <see cref="Invoice"/>, so a worksheet maps to exactly one spending authorization — which
/// is what makes the PO drawdown meaningful and lets a client's AP match invoice to PO.
/// <para>
/// <see cref="PoNumber"/> is null only when neither the trips nor the contract named a PO;
/// that work still gets its own draft rather than being dropped.
/// <see cref="PurchaseOrder"/> is null whenever no replica row matched the number — the
/// contract rate then priced every line.
/// </para>
/// </summary>
public sealed record InvoiceDraft(
    ContractSnapshot Contract,
    string? PoNumber,
    PurchaseOrderSnapshot? PurchaseOrder,
    IReadOnlyList<InvoiceLine> Lines,
    IReadOnlyList<Guid> ClaimedTripIds,
    IReadOnlyList<string> Warnings)
{
    /// <summary>The draft's own total — the plain sum of its lines, with no tax uplift.</summary>
    public decimal TotalCad => Math.Round(Lines.Sum(line => line.AmountCad), 2);
}

/// <summary>
/// Pure draft-invoice pricing — no I/O, fully unit-testable. Resolves the contract covering
/// the period, filters to completed, uninvoiced trips inside it, and prices <b>per purchase
/// order</b>: each PO is negotiated separately, so it carries its own round-trip and one-way
/// rates, and the contract rate is only the fallback.
///
/// <para><b>The pricing rule</b> (mirrored exactly by the Dispatcher's estimator):</para>
/// <code>
/// contractRate = contract snapshot's RatePerRoundTripCad        (resolved as before)
/// group eligible legs by (effective PO number, RoundTripKey)
///   effective PO number = trip.PoNumber ?? contract.DefaultPoNumber
/// per group:
///   po            = PO snapshot for that client + PO number, may be null
///   roundTripRate = po?.RoundTripRateCad ?? contractRate
///   oneWayRate    = po?.OneWayRateCad    ?? roundTripRate * 0.5m
///   if the group has an Outbound leg AND an Inbound leg
///       → ONE line, Quantity 1, UnitPriceCad = roundTripRate
///   else
///       → one line PER LEG, Quantity 1, UnitPriceCad = oneWayRate, review-flagged
/// </code>
///
/// Grouping by PO is what implements "a round trip must be a single PO": two legs sharing a
/// RoundTripKey but booked on different POs land in different groups, so each prices as a
/// lone one-way leg and both carry <see cref="SplitPurchaseOrderFlag"/>.
///
/// <para>
/// Note the change from the original rule: an unpaired leg is now
/// <c>Quantity 1 × oneWayRate</c> rather than <c>Quantity 0.5 × roundTripRate</c>. Where no
/// explicit one-way rate exists the amount is identical (<c>0.5 × r == 1 × 0.5r</c>); only
/// the quantity/unit-price columns change, which is the honest shape now that a one-way rate
/// is a real negotiated figure. <see cref="InvoiceLine.AmountCad"/> stays exactly
/// <c>Math.Round(Quantity × UnitPriceCad, 2)</c>.
/// </para>
///
/// <para>
/// <b>Warnings never block.</b> A leg whose ServiceDate falls outside its PO's
/// [Issued, Expiry] window is flagged on its line; a draft whose total plus what has already
/// been invoiced against that PO exceeds the PO's authorized value produces a draft-level
/// warning. Neither refuses to price or invoice.
/// </para>
///
/// A pair whose legs include a deadhead (empty repositioning) leg still prices at the full
/// round-trip rate, but the line is flagged so the dispatcher can apply an optional manual
/// discount — worksheet lines are editable. Trips with no key (ad-hoc/charter/cargo) are left
/// unclaimed for manual lines. No tax line is ever built: the platform applies no tax at all,
/// and QuickBooks Online owns that calculation.
///
/// <para>
/// <b>Seam — Manual-billing contracts.</b> The builder still refuses a contract that is not
/// <see cref="ContractSnapshot.RoundTripRateBillingModel"/>, even when a PO carries explicit
/// rates: Manual stays manual by decision. Enabling PO-driven drafting for such a client
/// later is a change to the single guard below (accept a Manual contract when every group's
/// PO supplies its own rate, and treat the missing <c>contractRate</c> as "no fallback")
/// — the per-group rate resolution already needs no contract rate when the PO sets one.
/// </para>
/// </summary>
public static class InvoiceDraftBuilder
{
    /// <summary>
    /// A leg billed on its own rather than as half of a paired round trip — a lone leg, a
    /// missing direction, two legs sharing a direction, or legs split across POs. Priced at
    /// the effective one-way rate, quantity 1.
    /// </summary>
    public const string OneWayLegFlag = "one-way leg — review";

    /// <summary>
    /// Two legs shared a RoundTripKey but were booked on different purchase orders, so they
    /// are not a round trip: each prices one-way on its own PO's worksheet.
    /// </summary>
    public const string SplitPurchaseOrderFlag = "round-trip legs on different POs — priced one-way, review";

    /// <summary>A priced leg's service date falls outside its PO's issue/expiry window. Flagged, never refused.</summary>
    public const string OutsidePurchaseOrderWindowFlag = "outside PO window — review";

    /// <summary>
    /// This draft plus everything already invoiced against the PO exceeds the PO's authorized
    /// value. A draft-level warning (see <see cref="InvoiceDraft.Warnings"/>), not a line flag
    /// — and never a refusal.
    /// </summary>
    public const string PurchaseOrderValueExceededFlag = "PO value exceeded — review";

    public const string DeadheadReturnFlag = "round trip incl. deadhead return — discount optional";

    /// <summary>
    /// Prices a client's period into one draft per effective PO. All-or-nothing: the first
    /// failure aborts the whole operation, matching the original single-draft semantics —
    /// a partially generated set of worksheets would be worse than none.
    /// </summary>
    /// <param name="purchaseOrders">The client's PO replica rows. Empty/null means every line prices at the contract rate.</param>
    /// <param name="invoicedTotalsByPoNumber">
    /// Non-Void invoiced totals already booked against each PO number, for the value warning.
    /// Read on the Billing side from <c>rm_invoices</c>; absent entries count as zero.
    /// </param>
    public static Result<IReadOnlyList<InvoiceDraft>> Build(
        IReadOnlyList<ContractSnapshot> clientContracts,
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<BillableTrip> billableTrips,
        IReadOnlyList<PurchaseOrderSnapshot>? purchaseOrders = null,
        IReadOnlyDictionary<string, decimal>? invoicedTotalsByPoNumber = null)
    {
        if (periodEnd < periodStart)
        {
            return Result.Failure<IReadOnlyList<InvoiceDraft>>(InvoiceErrors.InvalidPeriod);
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
            return Result.Failure<IReadOnlyList<InvoiceDraft>>(InvoiceErrors.NoActiveContract);
        }

        // The Manual-stays-manual guard, and the seam described in the type doc.
        if (contract.BillingModel != ContractSnapshot.RoundTripRateBillingModel
            || contract.RatePerRoundTripCad is not { } contractRate)
        {
            return Result.Failure<IReadOnlyList<InvoiceDraft>>(InvoiceErrors.NotRoundTripBilled);
        }

        // Defence in depth: the repository already scopes to client + period + uninvoiced,
        // but the builder re-filters so its own contract is airtight under test.
        var eligible = billableTrips
            .Where(t => t.IsUninvoiced
                && t.RoundTripKey is not null
                && t.ServiceDate >= periodStart
                && t.ServiceDate <= periodEnd)
            .ToList();

        var contractPoNumber = Normalize(contract.DefaultPoNumber);

        // RoundTripKeys whose legs are booked on more than one PO: they cannot be a round
        // trip, and every leg of such a key says so on its line.
        var splitKeys = eligible
            .GroupBy(t => t.RoundTripKey!, StringComparer.Ordinal)
            .Where(g => g.Select(t => PoBucket(t, contractPoNumber)).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var drafts = new List<InvoiceDraft>();

        var poGroups = eligible
            .GroupBy(t => PoBucket(t, contractPoNumber), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var poGroup in poGroups)
        {
            // The bucket key is a normalized-for-matching form; the display/stamp value is the
            // number as it was actually typed on the first leg (or the contract default).
            var poNumber = poGroup
                .Select(t => Normalize(t.PoNumber) ?? contractPoNumber)
                .FirstOrDefault(n => n is not null);

            var purchaseOrder = ResolvePurchaseOrder(purchaseOrders, poNumber);
            var roundTripRate = purchaseOrder?.RoundTripRateCad ?? contractRate;
            var oneWayRate = purchaseOrder?.OneWayRateCad ?? roundTripRate * 0.5m;

            var draftResult = BuildForPurchaseOrder(
                contract,
                poNumber,
                purchaseOrder,
                [.. poGroup],
                splitKeys,
                roundTripRate,
                oneWayRate,
                invoicedTotalsByPoNumber);

            if (draftResult.IsFailure)
            {
                return Result.Failure<IReadOnlyList<InvoiceDraft>>(draftResult.Error);
            }

            drafts.Add(draftResult.Value);
        }

        // Nothing eligible at all still yields exactly one empty worksheet on the contract's
        // default PO — that zero-line draft is the manual-lines starting point, and it is how
        // generation has always behaved for a client with no completed trips.
        if (drafts.Count == 0)
        {
            drafts.Add(new InvoiceDraft(contract, contractPoNumber, ResolvePurchaseOrder(purchaseOrders, contractPoNumber), [], [], []));
        }

        // Drafts with a PO first, in PO-number order; the no-PO worksheet last. Deterministic,
        // which also makes the per-draft invoice numbering deterministic.
        return Result.Success<IReadOnlyList<InvoiceDraft>>(
            [.. drafts
                .OrderBy(d => d.PoNumber is null)
                .ThenBy(d => d.PoNumber, StringComparer.Ordinal)]);
    }

    private static Result<InvoiceDraft> BuildForPurchaseOrder(
        ContractSnapshot contract,
        string? poNumber,
        PurchaseOrderSnapshot? purchaseOrder,
        IReadOnlyList<BillableTrip> trips,
        HashSet<string> splitKeys,
        decimal roundTripRate,
        decimal oneWayRate,
        IReadOnlyDictionary<string, decimal>? invoicedTotalsByPoNumber)
    {
        var lines = new List<InvoiceLine>();
        var claimed = new List<Guid>();

        var groups = trips
            .GroupBy(t => t.RoundTripKey!, StringComparer.Ordinal)
            .OrderBy(g => g.Min(t => t.ServiceDate))
            .ThenBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var legs = group.OrderBy(t => t.CompletedAtUtc).ToList();
            var serviceDate = legs.Min(t => t.ServiceDate);

            // A group is a complete round trip only when it pairs an Outbound leg with an
            // Inbound one — never merely on leg count. Same-direction or direction-less legs
            // are not a valid pair and each bills as a one-way leg.
            var hasOutbound = legs.Any(t => IsDirection(t, "Outbound"));
            var hasInbound = legs.Any(t => IsDirection(t, "Inbound"));

            if (hasOutbound && hasInbound)
            {
                var first = legs[0];
                var flags = new List<string>();
                if (legs.Any(t => t.IsEmptyLeg))
                {
                    flags.Add(DeadheadReturnFlag);
                }

                if (legs.Any(leg => IsOutsideWindow(purchaseOrder, leg)))
                {
                    flags.Add(OutsidePurchaseOrderWindowFlag);
                }

                var lineResult = InvoiceLine.Create(
                    Describe("Corridor round trip", first.RouteName, serviceDate, flags),
                    legs.Select(t => t.Id).ToList(),
                    null,
                    serviceDate,
                    quantity: 1m,
                    unitPriceCad: roundTripRate);

                if (lineResult.IsFailure)
                {
                    return Result.Failure<InvoiceDraft>(lineResult.Error);
                }

                lines.Add(lineResult.Value);
                claimed.AddRange(legs.Select(t => t.Id));
            }
            else
            {
                // Lone leg, missing direction, duplicated direction, or a pair broken apart by
                // its legs sitting on different POs: each present leg prices at the effective
                // one-way rate on its own review-flagged line.
                foreach (var leg in legs)
                {
                    var flags = new List<string>();
                    if (splitKeys.Contains(group.Key))
                    {
                        flags.Add(SplitPurchaseOrderFlag);
                    }

                    if (IsOutsideWindow(purchaseOrder, leg))
                    {
                        flags.Add(OutsidePurchaseOrderWindowFlag);
                    }

                    var lineResult = InvoiceLine.Create(
                        Describe(OneWayLegFlag, leg.RouteName, leg.ServiceDate, flags),
                        [leg.Id],
                        leg.TripNumber,
                        leg.ServiceDate,
                        quantity: 1m,
                        unitPriceCad: oneWayRate);

                    if (lineResult.IsFailure)
                    {
                        return Result.Failure<InvoiceDraft>(lineResult.Error);
                    }

                    lines.Add(lineResult.Value);
                    claimed.Add(leg.Id);
                }
            }
        }

        var total = Math.Round(lines.Sum(line => line.AmountCad), 2);
        var warnings = new List<string>();

        if (purchaseOrder is { AmountCad: { } authorized } && poNumber is not null)
        {
            var alreadyInvoiced = InvoicedAgainst(invoicedTotalsByPoNumber, poNumber);
            if (alreadyInvoiced + total > authorized)
            {
                warnings.Add(
                    $"{PurchaseOrderValueExceededFlag} · PO {poNumber} · authorized "
                    + $"{Money(authorized)}, already invoiced {Money(alreadyInvoiced)}, this draft {Money(total)}");
            }
        }

        return Result.Success(new InvoiceDraft(contract, poNumber, purchaseOrder, lines, claimed, warnings));
    }

    /// <summary>
    /// The grouping key for a leg's effective PO. Case-insensitively normalized so the same
    /// number typed two ways still lands on one worksheet; the empty string is the "no PO at
    /// all" bucket, which sorts first here and is reordered to last among the finished drafts.
    /// </summary>
    private static string PoBucket(BillableTrip trip, string? contractPoNumber) =>
        (Normalize(trip.PoNumber) ?? contractPoNumber)?.ToUpperInvariant() ?? string.Empty;

    /// <summary>
    /// The PO replica row for a number, matched case-insensitively. When a client has more
    /// than one row for the same number (a re-issue), the most recently issued wins — the
    /// current terms are the ones that were negotiated last.
    /// </summary>
    private static PurchaseOrderSnapshot? ResolvePurchaseOrder(
        IReadOnlyList<PurchaseOrderSnapshot>? purchaseOrders,
        string? poNumber)
    {
        if (poNumber is null || purchaseOrders is null)
        {
            return null;
        }

        return purchaseOrders
            .Where(p => string.Equals(p.PoNumber?.Trim(), poNumber, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Issued)
            .ThenByDescending(p => p.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsOutsideWindow(PurchaseOrderSnapshot? purchaseOrder, BillableTrip leg) =>
        purchaseOrder is not null && !purchaseOrder.CoversDate(leg.ServiceDate);

    private static decimal InvoicedAgainst(
        IReadOnlyDictionary<string, decimal>? invoicedTotalsByPoNumber,
        string poNumber)
    {
        if (invoicedTotalsByPoNumber is null)
        {
            return 0m;
        }

        // The caller's dictionary may or may not be case-insensitive, so match both ways.
        if (invoicedTotalsByPoNumber.TryGetValue(poNumber, out var exact))
        {
            return exact;
        }

        foreach (var (key, value) in invoicedTotalsByPoNumber)
        {
            if (string.Equals(key?.Trim(), poNumber, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return 0m;
    }

    private static string Describe(string headline, string routeName, DateOnly serviceDate, List<string> flags)
    {
        var segments = new List<string> { headline, routeName, serviceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        segments.AddRange(flags);
        return string.Join(" · ", segments);
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsDirection(BillableTrip trip, string direction) =>
        string.Equals(trip.Direction, direction, StringComparison.OrdinalIgnoreCase);
}
