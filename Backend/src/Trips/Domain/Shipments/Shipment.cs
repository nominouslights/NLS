using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Shipments.Events;

namespace NorthernLink.Trips.Domain.Shipments;

/// <summary>
/// One consignment of goods, tracked end to end. A shipment exists <b>before any trip does</b>
/// (pre-registration), rides one or more trips as ordered <see cref="Legs"/> (a hub transfer is
/// two legs), and is billed as a single item on delivery.
/// <para>
/// <b>The invariant this whole aggregate exists to protect:</b> <see cref="ClientId"/> is the
/// shipment's own and is <em>never</em> derived from, defaulted to, or required to match the
/// client of any trip that carries it. A run for Alamos is routinely full of another company's
/// freight, and one trip can carry parcels for several clients at once. Nothing in
/// <see cref="AddLeg"/> or anywhere else may touch the client — that is why cargo bills on its
/// own invoice rather than as lines on the trip's, and why copying a trip's client onto a
/// shipment would be a silent mis-billing rather than a convenience.
/// </para>
/// <para>
/// The lifecycle mirrors <c>Trip</c>: operational up to handover, then billing-driven.
/// <see cref="RecordDelivery"/> is the "the goods arrived" signal and picks its landing status
/// from <see cref="IsBillable"/> — a shipment with a client and a charge lands in
/// <see cref="ShipmentStatus.ReadyForBilling"/> (raising
/// <see cref="ShipmentReadyForBillingDomainEvent"/>, Billing's feed), and one without — a
/// counter sale already paid, or a goodwill carry — lands in
/// <see cref="ShipmentStatus.Delivered"/> and never enters the billing arc. From there
/// <see cref="MarkInvoiced"/>, <see cref="MarkPaid"/>, and <see cref="WriteOff"/> are driven
/// only by Billing's invoice events, never by hand.
/// </para>
/// <para>
/// <b>A shipment's billing is fully independent of its trips' billing.</b> A trip being
/// cancelled, written off, or closed without billing says nothing about whether the freight it
/// carried is collectable, and nothing here reacts to those. The one coupling that does exist
/// is operational: a cancelled trip releases its <em>planned</em> legs
/// (<see cref="ReleaseFromTrip"/>) so the goods go back in the queue to be re-routed. This is
/// stated explicitly because it looks like a missing link otherwise, and "fixing" it would
/// break the cross-client case.
/// </para>
/// </summary>
public sealed class Shipment : AggregateRoot, ITenantScoped
{
    private readonly List<ShipmentLeg> _legs = [];

    private Shipment()
    {
        // EF Core materialization only.
        ShipmentNumber = null!;
        Description = null!;
        OriginName = null!;
        DestinationName = null!;
    }

    public Guid TenantId { get; private set; }

    public string ShipmentNumber { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public ShipmentKind Kind { get; private set; }

    // --- Its OWN client. See the class comment; this is the load-bearing invariant. ---
    public Guid? ClientId { get; private set; }
    public string? ClientName { get; private set; }
    public string? PoNumber { get; private set; }

    // Parties.
    public string? ConsignorName { get; private set; }
    public string? ConsignorContact { get; private set; }
    public string? ConsigneeName { get; private set; }
    public string? ConsigneeContact { get; private set; }

    // End-to-end corridor. Individual legs carry their own from/to.
    public Guid? OriginStopId { get; private set; }
    public string OriginName { get; private set; }
    public Guid? DestinationStopId { get; private set; }
    public string DestinationName { get; private set; }

    // What.
    public string Description { get; private set; }
    public int Pieces { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? LengthCm { get; private set; }
    public decimal? WidthCm { get; private set; }
    public decimal? HeightCm { get; private set; }
    public bool Hazmat { get; private set; }
    public decimal? DeclaredValueCad { get; private set; }
    public string? SpecialHandling { get; private set; }

    /// <summary>
    /// Tied down for transport. Per-item, and distinct from the manifest's trip-level
    /// "all cargo secured" attestation — that one is the driver signing for the whole load.
    /// </summary>
    public bool Secured { get; private set; }

    // Money.
    public decimal? ChargeCad { get; private set; }
    public ShipmentPaymentMethod? PaymentMethod { get; private set; }
    public DateTimeOffset? PaymentCollectedAtUtc { get; private set; }

    // Pre-registration window.
    public DateOnly? ReadyDate { get; private set; }
    public DateOnly? RequiredByDate { get; private set; }

    /// <summary>The trips this shipment rides, in order. A child table, not jsonb — see <see cref="ShipmentLeg"/>.</summary>
    public IReadOnlyList<ShipmentLeg> Legs => _legs.AsReadOnly();

    // Handover.
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public string? ReceivedBy { get; private set; }
    public string? DeliveryNote { get; private set; }

    public string? CancelledReason { get; private set; }
    public string? WrittenOffReason { get; private set; }

    // Provenance — stamped on register and re-stamped on every edit.
    public ShipmentSource Source { get; private set; }
    public string? EnteredBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Whether handing this over starts a billing arc. Both halves matter: no client means a
    /// counter sale settled at the desk, and no charge means a goodwill carry. Neither has
    /// anything to invoice.
    /// </summary>
    public bool IsBillable => ClientId is not null && ChargeCad is > 0m;

    /// <summary>The goods have been handed over (or written off) — routing is history now.</summary>
    public bool IsOperationallyClosed =>
        Status is not (ShipmentStatus.Registered or ShipmentStatus.Assigned or ShipmentStatus.InTransit);

    /// <summary>Genuinely final — no outgoing transitions at all.</summary>
    public bool IsFinal => Status is ShipmentStatus.Cancelled or ShipmentStatus.WrittenOff;

    /// <summary>
    /// Freight sitting at a hub: picked up and dropped on an earlier leg, with a later leg still
    /// planned and not yet loaded. Real inventory in a real building, and invisible unless a
    /// screen asks for exactly this — which is why it is a first-class derivation rather than
    /// something the UI is left to work out.
    /// </summary>
    public bool IsAwaitingTransfer =>
        Status == ShipmentStatus.InTransit
        && _legs.Any(l => l.Status == ShipmentLegStatus.Planned)
        && _legs.All(l => l.Status != ShipmentLegStatus.PickedUp);

    /// <summary>The leg currently responsible for the goods — the first one not yet finished.</summary>
    public ShipmentLeg? CurrentLeg => _legs.FirstOrDefault(l => !l.IsFinished);

    /// <summary>
    /// The lifecycle transition matrix. Diagonal (same status) is not a transition.
    /// <para>
    /// Registered reaches Delivered directly because a counter drop-off collected by hand never
    /// rides a trip at all. Delivered ⇄ ReadyForBilling both ways: a walk-up parcel later
    /// attributed to a client becomes billable, and a client attached in error can be cleared
    /// again right up until an invoice claims it. Settled → Invoiced is legal (a payment
    /// confirmation cleared in error), which is exactly why the return-path handler needs a
    /// high-water mark — this matrix alone cannot stop a stale replay un-settling a paid
    /// shipment.
    /// </para>
    /// </summary>
    public static bool CanTransition(ShipmentStatus from, ShipmentStatus to) =>
        (from, to) switch
        {
            (ShipmentStatus.Registered, ShipmentStatus.Assigned) => true,
            (ShipmentStatus.Assigned, ShipmentStatus.Registered) => true,
            (ShipmentStatus.Assigned, ShipmentStatus.InTransit) => true,

            (ShipmentStatus.Registered or ShipmentStatus.Assigned or ShipmentStatus.InTransit,
                ShipmentStatus.Delivered or ShipmentStatus.ReadyForBilling or ShipmentStatus.Cancelled) => true,

            (ShipmentStatus.Delivered, ShipmentStatus.ReadyForBilling) => true,
            (ShipmentStatus.ReadyForBilling, ShipmentStatus.Delivered) => true,

            (ShipmentStatus.ReadyForBilling, ShipmentStatus.Invoiced or ShipmentStatus.WrittenOff) => true,
            (ShipmentStatus.Invoiced, ShipmentStatus.Settled or ShipmentStatus.WrittenOff) => true,
            (ShipmentStatus.Settled, ShipmentStatus.Invoiced) => true,

            _ => false,
        };

    /// <summary>
    /// Records cargo. Valid with nothing but a description — no trip, no client, no charge, no
    /// dates. That is the point: pre-registration is a dispatcher writing down what turned up at
    /// the counter, long before anyone knows which run will take it.
    /// </summary>
    public static Result<Shipment> Register(
        Guid tenantId,
        string shipmentNumber,
        ShipmentDetails details,
        ShipmentSource source,
        string? enteredBy)
    {
        if (string.IsNullOrWhiteSpace(shipmentNumber))
        {
            return Result.Failure<Shipment>(ShipmentErrors.ShipmentNumberRequired);
        }

        var validation = Validate(details, source, enteredBy);
        if (validation.IsFailure)
        {
            return Result.Failure<Shipment>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var shipment = new Shipment
        {
            TenantId = tenantId,
            ShipmentNumber = shipmentNumber.Trim(),
            Status = ShipmentStatus.Registered,
            Secured = true,
            Source = source,
            EnteredBy = StampEnteredBy(source, enteredBy),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        shipment.Apply(details);
        shipment.PaymentCollectedAtUtc = details.PaymentMethod is not null ? now : null;

        shipment.Raise(new ShipmentRegisteredDomainEvent(shipment.Id));
        return Result.Success(shipment);
    }

    /// <summary>
    /// Revises the shipment in place and re-stamps provenance. Routing, handover, and the
    /// billing-driven statuses move through their own methods; the shipment number never
    /// changes.
    /// </summary>
    public Result UpdateDetails(ShipmentDetails details, ShipmentSource source, string? enteredBy)
    {
        if (IsFinal)
        {
            return Result.Failure(ShipmentErrors.NotEditable);
        }

        var validation = Validate(details, source, enteredBy);
        if (validation.IsFailure)
        {
            return validation;
        }

        // Once an invoice speaks for the shipment, its money is the invoice's business. Everything
        // descriptive stays editable — correcting a consignee's name on an invoiced parcel is
        // routine and harmless.
        if (BillingIsLocked && BillingDiffers(details))
        {
            return Result.Failure(ShipmentErrors.BillingNotEditable);
        }

        var wasBillable = IsBillable;
        Apply(details);
        Source = source;
        EnteredBy = StampEnteredBy(source, enteredBy);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new ShipmentUpdatedDomainEvent(Id, source, EnteredBy));
        SettleBillingStatus(wasBillable);
        return Result.Success();
    }

    /// <summary>
    /// Routes the shipment through another trip, appended after any legs already planned. Two
    /// legs is a hub transfer — out on one run, onward on the next — still one billable item.
    /// <para>
    /// <b>This never touches <see cref="ClientId"/>.</b> The trip's client and the shipment's are
    /// unrelated facts, and defaulting one from the other is the mis-billing this aggregate is
    /// built to prevent.
    /// </para>
    /// <para>
    /// Deliberately no empty-leg guard: a deadhead is an empty <em>passenger</em> leg, and
    /// filling a repositioning run with freight is the entire economic point of having one.
    /// (<c>TripErrors.ManifestNotAllowedForEmptyLeg</c> still forbids a passenger manifest on
    /// one — the asymmetry is intended.)
    /// </para>
    /// </summary>
    public Result AddLeg(
        Guid tripId,
        string tripNumber,
        DateOnly tripServiceDate,
        Guid? fromStopId,
        string? fromName,
        Guid? toStopId,
        string? toName)
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(ShipmentErrors.OperationallyClosed(Status));
        }

        if (string.IsNullOrWhiteSpace(tripNumber))
        {
            return Result.Failure(ShipmentErrors.TripNumberRequired);
        }

        // Riding the same trip twice is always a mis-click: a single run cannot both carry the
        // goods and receive them onward.
        if (_legs.Any(l => l.TripId == tripId))
        {
            return Result.Failure(ShipmentErrors.LegAlreadyOnTrip);
        }

        _legs.Add(ShipmentLeg.Create(
            TenantId,
            Id,
            _legs.Count + 1,
            tripId,
            tripNumber,
            tripServiceDate,
            fromStopId,
            fromName,
            toStopId,
            toName));

        if (Status == ShipmentStatus.Registered)
        {
            Status = ShipmentStatus.Assigned;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ShipmentRoutingChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Drops a planned leg from the route and closes the numbering gap. Only a planned leg goes:
    /// once freight has physically been on a truck, that stays on the record.
    /// </summary>
    public Result RemoveLeg(int sequence)
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(ShipmentErrors.OperationallyClosed(Status));
        }

        if (_legs.SingleOrDefault(l => l.Sequence == sequence) is not { } leg)
        {
            return Result.Failure(ShipmentErrors.LegNotFound);
        }

        if (leg.Status != ShipmentLegStatus.Planned)
        {
            return Result.Failure(ShipmentErrors.LegNotRemovable);
        }

        _legs.Remove(leg);
        Resequence();
        RelaxStatusAfterRoutingChange();

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ShipmentRoutingChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// Releases every <em>planned</em> leg on a given trip — the reaction to that trip being
    /// cancelled. Legs already picked up or dropped are left alone: the freight physically moved,
    /// and that is history.
    /// <para>
    /// The goods themselves are deliberately not cancelled. They still need to get there, and
    /// putting them back in the unassigned queue is the dispatcher's actual next action.
    /// Idempotent — a second pass finds nothing planned.
    /// </para>
    /// </summary>
    public Result ReleaseFromTrip(Guid tripId)
    {
        var released = _legs
            .Where(l => l.TripId == tripId && l.Status == ShipmentLegStatus.Planned)
            .ToList();

        if (released.Count == 0)
        {
            return Result.Success();
        }

        foreach (var leg in released)
        {
            _legs.Remove(leg);
        }

        Resequence();
        RelaxStatusAfterRoutingChange();

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ShipmentRoutingChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>Records the freight going onto one leg's trip. Legs run in order.</summary>
    public Result RecordLegPickup(int sequence, DateTimeOffset? atUtc, string? by)
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(ShipmentErrors.OperationallyClosed(Status));
        }

        if (_legs.SingleOrDefault(l => l.Sequence == sequence) is not { } leg)
        {
            return Result.Failure(ShipmentErrors.LegNotFound);
        }

        if (leg.Status != ShipmentLegStatus.Planned)
        {
            return Result.Failure(ShipmentErrors.LegNotPlanned);
        }

        if (_legs.Any(l => l.Sequence < sequence && !l.IsFinished))
        {
            return Result.Failure(ShipmentErrors.EarlierLegOutstanding);
        }

        leg.MarkPickedUp(atUtc ?? DateTimeOffset.UtcNow, by);

        if (Status == ShipmentStatus.Assigned)
        {
            Status = ShipmentStatus.InTransit;
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ShipmentLegPickedUpDomainEvent(Id, sequence));
        return Result.Success();
    }

    /// <summary>
    /// Records the freight coming off one leg's trip — at a hub, or at the destination. This is
    /// not the handover: <see cref="RecordDelivery"/> is what a consignee signing for the goods
    /// produces, and only that earns the charge.
    /// </summary>
    public Result RecordLegDrop(int sequence, DateTimeOffset? atUtc, string? by)
    {
        if (IsOperationallyClosed)
        {
            return Result.Failure(ShipmentErrors.OperationallyClosed(Status));
        }

        if (_legs.SingleOrDefault(l => l.Sequence == sequence) is not { } leg)
        {
            return Result.Failure(ShipmentErrors.LegNotFound);
        }

        if (leg.Status != ShipmentLegStatus.PickedUp)
        {
            return Result.Failure(ShipmentErrors.LegNotPickedUp);
        }

        leg.MarkDropped(atUtc ?? DateTimeOffset.UtcNow, by);

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ShipmentLegDroppedDomainEvent(Id, sequence));
        return Result.Success();
    }

    /// <summary>
    /// The goods reached the consignee — the only path out of the operational half of the
    /// lifecycle, and the exact mirror of <c>Trip.FinishOperations</c>. Where it lands depends on
    /// whether anyone will be invoiced: <see cref="IsBillable"/> sends it to
    /// <see cref="ShipmentStatus.ReadyForBilling"/> and raises Billing's feed; anything else goes
    /// to <see cref="ShipmentStatus.Delivered"/> and never enters the billing arc.
    /// <para>
    /// Outstanding legs are closed out rather than refused. A dispatcher asserting the consignee
    /// has the goods makes any remaining leg moot by definition, and the codebase already takes
    /// this line elsewhere — <c>Trip.FinishOperations</c> accepts a direct Scheduled → finish
    /// because dispatchers forget to press START, and the real guard was never the intermediate
    /// status.
    /// </para>
    /// </summary>
    public Result RecordDelivery(DateTimeOffset? atUtc, string? receivedBy, string? note)
    {
        var target = IsBillable ? ShipmentStatus.ReadyForBilling : ShipmentStatus.Delivered;
        if (!CanTransition(Status, target))
        {
            return Result.Failure(ShipmentErrors.InvalidStatusTransition(Status, target));
        }

        var at = atUtc ?? DateTimeOffset.UtcNow;
        foreach (var leg in _legs.Where(l => !l.IsFinished))
        {
            leg.MarkDropped(at, receivedBy);
        }

        Status = target;
        DeliveredAtUtc = at;
        ReceivedBy = Normalize(receivedBy);
        DeliveryNote = Normalize(note);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(target == ShipmentStatus.ReadyForBilling
            ? new ShipmentReadyForBillingDomainEvent(Id)
            : new ShipmentDeliveredDomainEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Attaches (or clears) the party who pays and what they pay — the only narrow command for
    /// it, and the path every backfilled legacy cargo row has to travel, since the old jsonb
    /// never recorded a client at all.
    /// <para>
    /// If this makes an already-delivered shipment billable it moves to
    /// <see cref="ShipmentStatus.ReadyForBilling"/> and raises Billing's feed; if it corrects the
    /// charge on one already waiting, it re-raises so Billing refreshes its uninvoiced replica;
    /// if it clears the client before an invoice claimed it, the shipment falls back to
    /// <see cref="ShipmentStatus.Delivered"/>. Refused outright once an invoice speaks for it.
    /// </para>
    /// </summary>
    public Result SetBilling(
        Guid? clientId,
        string? clientName,
        string? poNumber,
        decimal? chargeCad,
        ShipmentPaymentMethod? paymentMethod)
    {
        if (IsFinal)
        {
            return Result.Failure(ShipmentErrors.NotEditable);
        }

        if (BillingIsLocked)
        {
            return Result.Failure(ShipmentErrors.BillingNotEditable);
        }

        var validation = ValidateBilling(clientId, clientName, chargeCad, paymentMethod);
        if (validation.IsFailure)
        {
            return validation;
        }

        var wasBillable = IsBillable;

        ClientId = clientId;
        ClientName = clientId is null ? null : clientName!.Trim();
        PoNumber = Normalize(poNumber);
        ChargeCad = chargeCad;
        PaymentMethod = paymentMethod;
        PaymentCollectedAtUtc = paymentMethod is null ? null : PaymentCollectedAtUtc ?? DateTimeOffset.UtcNow;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        SettleBillingStatus(wasBillable);
        return Result.Success();
    }

    /// <summary>Per-item "tied down" flag, set from the manifest's cargo section or the app.</summary>
    public Result MarkSecured(bool secured)
    {
        if (IsFinal)
        {
            return Result.Failure(ShipmentErrors.NotEditable);
        }

        Secured = secured;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Cancel(string? reason)
    {
        if (!CanTransition(Status, ShipmentStatus.Cancelled))
        {
            return Result.Failure(ShipmentErrors.InvalidStatusTransition(Status, ShipmentStatus.Cancelled));
        }

        Status = ShipmentStatus.Cancelled;
        CancelledReason = Normalize(reason);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new ShipmentStatusChangedDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// The dispatcher's escape for freight that will never be invoiced — a client with no
    /// contract, a goodwill carry decided after the fact, a charge written off before any
    /// worksheet existed. Without it <see cref="ShipmentStatus.ReadyForBilling"/> would be a
    /// state with no exit, since every other way out is driven by an invoice that is never going
    /// to exist.
    /// </summary>
    public Result CloseWithoutBilling(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(ShipmentErrors.WriteOffReasonRequired);
        }

        if (!CanTransition(Status, ShipmentStatus.WrittenOff))
        {
            return Result.Failure(ShipmentErrors.InvalidStatusTransition(Status, ShipmentStatus.WrittenOff));
        }

        Status = ShipmentStatus.WrittenOff;
        WrittenOffReason = reason.Trim();
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        // Not the billing-state event: this write-off originates here, so Billing has to be told
        // to drop the shipment from its uninvoiced pool.
        Raise(new ShipmentClosedWithoutBillingDomainEvent(Id));
        return Result.Success();
    }

    /// <summary>
    /// The shipment's worksheet has been keyed into QuickBooks. Driven only by Billing's
    /// <c>billing.invoice-billing-state-changed</c> event — never settable by hand. Also the path
    /// back from <see cref="ShipmentStatus.Settled"/> when a payment confirmation is cleared in
    /// error. Idempotent: a re-delivery while already Invoiced is a no-op success raising nothing.
    /// </summary>
    public Result MarkInvoiced() => ApplyBillingState(ShipmentStatus.Invoiced, null);

    /// <summary>Payment confirmed — the shipment is Settled. Driven only by Billing. Idempotent.</summary>
    public Result MarkPaid() => ApplyBillingState(ShipmentStatus.Settled, null);

    /// <summary>
    /// The invoice carrying this shipment was written off — the money is not coming. Driven only
    /// by Billing. Final; the claim is never released, so a written-off shipment can never be
    /// re-billed. Idempotent.
    /// </summary>
    public Result WriteOff(string? reason) => ApplyBillingState(ShipmentStatus.WrittenOff, reason);

    private Result ApplyBillingState(ShipmentStatus target, string? writtenOffReason)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        // A clientless shipment never enters the billing arc, so no invoice can speak for it —
        // a billing-driven transition arriving for one means a claim set has gone wrong upstream.
        if (ClientId is null)
        {
            return Result.Failure(ShipmentErrors.BillingStateOnClientlessShipment);
        }

        if (!CanTransition(Status, target))
        {
            return Result.Failure(ShipmentErrors.InvalidStatusTransition(Status, target));
        }

        Status = target;
        if (target == ShipmentStatus.WrittenOff)
        {
            WrittenOffReason = Normalize(writtenOffReason);
        }

        UpdatedAtUtc = DateTimeOffset.UtcNow;
        Raise(new ShipmentBillingStateChangedDomainEvent(Id, target));
        return Result.Success();
    }

    /// <summary>True once an invoice speaks for the shipment's money.</summary>
    private bool BillingIsLocked =>
        Status is ShipmentStatus.Invoiced or ShipmentStatus.Settled or ShipmentStatus.WrittenOff;

    private bool BillingDiffers(ShipmentDetails details) =>
        ClientId != details.ClientId
        || ChargeCad != details.ChargeCad
        || PoNumber != Normalize(details.PoNumber)
        || PaymentMethod != details.PaymentMethod;

    /// <summary>
    /// Moves a delivered shipment across the billing threshold in whichever direction its money
    /// just went, and re-raises the feed when a charge is corrected before an invoice claims it.
    /// Shared by <see cref="UpdateDetails"/> and <see cref="SetBilling"/> so the two can never
    /// disagree about what attaching a client means.
    /// </summary>
    private void SettleBillingStatus(bool wasBillable)
    {
        switch (Status)
        {
            case ShipmentStatus.Delivered when IsBillable:
                Status = ShipmentStatus.ReadyForBilling;
                Raise(new ShipmentReadyForBillingDomainEvent(Id));
                break;

            case ShipmentStatus.ReadyForBilling when !IsBillable:
                Status = ShipmentStatus.Delivered;
                Raise(new ShipmentDeliveredDomainEvent(Id));
                break;

            case ShipmentStatus.ReadyForBilling when wasBillable:
                // Still billable, but the numbers moved — Billing refreshes its uninvoiced row.
                Raise(new ShipmentReadyForBillingDomainEvent(Id));
                break;
        }
    }

    /// <summary>
    /// After legs are removed, fall back to the weakest status the remaining route supports.
    /// A shipment mid-route stays InTransit even with nothing planned — the goods are somewhere
    /// real, and that is precisely the stranded-at-a-hub case a dispatcher has to see.
    /// </summary>
    private void RelaxStatusAfterRoutingChange()
    {
        if (Status == ShipmentStatus.Assigned && _legs.Count == 0)
        {
            Status = ShipmentStatus.Registered;
            Raise(new ShipmentStatusChangedDomainEvent(Id));
        }
    }

    private void Resequence()
    {
        var sequence = 1;
        foreach (var leg in _legs.OrderBy(l => l.Sequence).ToList())
        {
            leg.Resequence(sequence++);
        }
    }

    private void Apply(ShipmentDetails details)
    {
        Kind = details.Kind;
        Description = details.Description.Trim();
        Pieces = details.Pieces;
        WeightKg = details.WeightKg;
        LengthCm = details.LengthCm;
        WidthCm = details.WidthCm;
        HeightCm = details.HeightCm;
        Hazmat = details.Hazmat;
        DeclaredValueCad = details.DeclaredValueCad;
        SpecialHandling = Normalize(details.SpecialHandling);

        ConsignorName = Normalize(details.ConsignorName);
        ConsignorContact = Normalize(details.ConsignorContact);
        ConsigneeName = Normalize(details.ConsigneeName);
        ConsigneeContact = Normalize(details.ConsigneeContact);

        OriginStopId = details.OriginStopId;
        OriginName = details.OriginName?.Trim() ?? string.Empty;
        DestinationStopId = details.DestinationStopId;
        DestinationName = details.DestinationName?.Trim() ?? string.Empty;

        ReadyDate = details.ReadyDate;
        RequiredByDate = details.RequiredByDate;

        ClientId = details.ClientId;
        ClientName = details.ClientId is null ? null : details.ClientName!.Trim();
        PoNumber = Normalize(details.PoNumber);
        ChargeCad = details.ChargeCad;
        PaymentMethod = details.PaymentMethod;
    }

    private static Result Validate(ShipmentDetails details, ShipmentSource source, string? enteredBy)
    {
        if (string.IsNullOrWhiteSpace(details.Description))
        {
            return Result.Failure(ShipmentErrors.DescriptionRequired);
        }

        if (details.Pieces < 1)
        {
            return Result.Failure(ShipmentErrors.InvalidPieces);
        }

        if (details.WeightKg is < 0m)
        {
            return Result.Failure(ShipmentErrors.InvalidWeight);
        }

        if (details.LengthCm is < 0m || details.WidthCm is < 0m || details.HeightCm is < 0m)
        {
            return Result.Failure(ShipmentErrors.InvalidDimensions);
        }

        if (details.DeclaredValueCad is { } declared
            && (declared < 0m || decimal.Round(declared, 2) != declared))
        {
            return Result.Failure(ShipmentErrors.InvalidDeclaredValue);
        }

        if (details.RequiredByDate is { } requiredBy && details.ReadyDate is { } ready && requiredBy < ready)
        {
            return Result.Failure(ShipmentErrors.RequiredByBeforeReady);
        }

        if (source == ShipmentSource.Dispatcher && string.IsNullOrWhiteSpace(enteredBy))
        {
            return Result.Failure(ShipmentErrors.EnteredByRequired);
        }

        return ValidateBilling(details.ClientId, details.ClientName, details.ChargeCad, details.PaymentMethod);
    }

    /// <summary>
    /// Money rules. A half-recorded charge is worse than none: an amount with no method cannot be
    /// reconciled and a method with no amount reads as unpaid — the same reasoning
    /// <c>TripManifest.ValidateFares</c> applies to a passenger's fare. Rounding is checked here
    /// because the column's precision truncates silently.
    /// </summary>
    private static Result ValidateBilling(
        Guid? clientId,
        string? clientName,
        decimal? chargeCad,
        ShipmentPaymentMethod? paymentMethod)
    {
        if (clientId is not null && string.IsNullOrWhiteSpace(clientName))
        {
            return Result.Failure(ShipmentErrors.ClientNameRequired);
        }

        if (chargeCad is { } charge && (charge < 0m || decimal.Round(charge, 2) != charge))
        {
            return Result.Failure(ShipmentErrors.InvalidCharge);
        }

        if (paymentMethod is not null && clientId is not null)
        {
            return Result.Failure(ShipmentErrors.PaymentMethodOnClientShipment);
        }

        return paymentMethod switch
        {
            null when clientId is null && chargeCad is > 0m =>
                Result.Failure(ShipmentErrors.PaymentMethodRequired),

            ShipmentPaymentMethod.Cash or ShipmentPaymentMethod.Online when chargeCad is null or <= 0m =>
                Result.Failure(ShipmentErrors.ChargeRequired),

            ShipmentPaymentMethod.Waived when chargeCad is > 0m =>
                Result.Failure(ShipmentErrors.WaivedChargeMustBeZero),

            _ => Result.Success(),
        };
    }

    private static string? StampEnteredBy(ShipmentSource source, string? enteredBy) =>
        source == ShipmentSource.Dispatcher ? enteredBy!.Trim() : Normalize(enteredBy);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
