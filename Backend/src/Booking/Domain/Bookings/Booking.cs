using NorthernLink.Shared.Kernel;
using NorthernLink.Booking.Domain.Bookings.Events;

namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>
/// A community shuttle booking: one customer reserving one or more seats
/// (<see cref="Passengers"/>) on a corridor for a service date. The corridor is a Trips
/// route referenced by id with a name snapshot — never a library reference. Status machine:
/// Unconfirmed → Confirmed (only from Unconfirmed), {Unconfirmed, Confirmed} → Cancelled;
/// Cancelled is terminal and read-only. <see cref="HoldExpiresAtUtc"/> is stamped once at
/// creation (created-at + the policy's seat-hold window): an Unconfirmed booking reserves
/// seats only while the hold is live; an expired hold stays listed but stops reserving —
/// derived per read, never flipped by a worker. Payment method/status are plain recorded
/// fields in this batch (no processor integration).
/// </summary>
public sealed class Booking : AggregateRoot, ITenantScoped
{
    private readonly List<BookingPassenger> _passengers = [];

    private Booking()
    {
        // EF Core materialization only.
        CustomerName = null!;
        CorridorName = null!;
        Pickup = null!;
        Dropoff = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }

    /// <summary>Customer name snapshot at booking time — display without a join.</summary>
    public string CustomerName { get; private set; }

    /// <summary>The Trips route this booking rides (see corridor_lookup replica).</summary>
    public Guid CorridorId { get; private set; }

    /// <summary>Corridor name snapshot at booking time.</summary>
    public string CorridorName { get; private set; }

    public DateOnly ServiceDate { get; private set; }
    public BookingStatus Status { get; private set; }
    public BookingLocation Pickup { get; private set; }
    public BookingLocation Dropoff { get; private set; }
    public IReadOnlyList<BookingPassenger> Passengers => _passengers.AsReadOnly();
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }

    /// <summary>When this booking's seat hold lapses (creation time + policy SeatHoldMinutes).</summary>
    public DateTimeOffset HoldExpiresAtUtc { get; private set; }

    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<Booking> Create(
        Guid tenantId,
        Guid customerId,
        string customerName,
        Guid corridorId,
        string corridorName,
        DateOnly serviceDate,
        BookingLocation pickup,
        BookingLocation dropoff,
        IReadOnlyList<BookingPassengerDetails> passengers,
        PaymentMethod paymentMethod,
        TimeSpan seatHold,
        string? notes)
    {
        var passengerValidation = ValidatePassengers(passengers);
        if (passengerValidation.IsFailure)
        {
            return Result.Failure<Booking>(passengerValidation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var booking = new Booking
        {
            TenantId = tenantId,
            CustomerId = customerId,
            CustomerName = customerName.Trim(),
            CorridorId = corridorId,
            CorridorName = corridorName.Trim(),
            ServiceDate = serviceDate,
            Status = BookingStatus.Unconfirmed,
            Pickup = pickup,
            Dropoff = dropoff,
            PaymentMethod = paymentMethod,
            PaymentStatus = PaymentStatus.Unpaid,
            HoldExpiresAtUtc = now + seatHold,
            Notes = Clean(notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        booking.ReplacePassengers(passengers);
        booking.Raise(new BookingCreatedDomainEvent(booking.Id, tenantId));
        return Result.Success(booking);
    }

    /// <summary>
    /// Edits the booking's details (locations, passengers, payment fields, notes). Allowed
    /// while Unconfirmed or Confirmed; a cancelled booking is read-only. Status, service
    /// date, corridor, and the seat hold are never edited here — cancel and rebook instead.
    /// </summary>
    public Result Update(
        BookingLocation pickup,
        BookingLocation dropoff,
        IReadOnlyList<BookingPassengerDetails> passengers,
        PaymentMethod paymentMethod,
        PaymentStatus paymentStatus,
        string? notes)
    {
        if (Status == BookingStatus.Cancelled)
        {
            return Result.Failure(BookingErrors.CancelledIsReadOnly);
        }

        var passengerValidation = ValidatePassengers(passengers);
        if (passengerValidation.IsFailure)
        {
            return passengerValidation;
        }

        Pickup = pickup;
        Dropoff = dropoff;
        PaymentMethod = paymentMethod;
        PaymentStatus = paymentStatus;
        Notes = Clean(notes);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        ReplacePassengers(passengers);

        Raise(new BookingUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>Unconfirmed → Confirmed. The only path to Confirmed.</summary>
    public Result Confirm()
    {
        if (Status == BookingStatus.Cancelled)
        {
            return Result.Failure(BookingErrors.CancelledIsReadOnly);
        }

        if (Status == BookingStatus.Confirmed)
        {
            return Result.Failure(BookingErrors.AlreadyConfirmed);
        }

        Status = BookingStatus.Confirmed;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BookingConfirmedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    /// <summary>{Unconfirmed, Confirmed} → Cancelled. Terminal.</summary>
    public Result Cancel()
    {
        if (Status == BookingStatus.Cancelled)
        {
            return Result.Failure(BookingErrors.AlreadyCancelled);
        }

        Status = BookingStatus.Cancelled;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BookingCancelledDomainEvent(Id, TenantId));
        return Result.Success();
    }

    private static Result ValidatePassengers(IReadOnlyList<BookingPassengerDetails> passengers)
    {
        if (passengers.Count == 0)
        {
            return Result.Failure(BookingErrors.AtLeastOnePassenger);
        }

        if (passengers.Any(p => string.IsNullOrWhiteSpace(p.Name)))
        {
            return Result.Failure(BookingErrors.PassengerNameRequired);
        }

        return Result.Success();
    }

    private void ReplacePassengers(IReadOnlyList<BookingPassengerDetails> passengers)
    {
        _passengers.Clear();
        _passengers.AddRange(passengers.Select(p =>
            BookingPassenger.Create(TenantId, p.Name!, p.Phone, p.IsBillingCustomer)));
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
