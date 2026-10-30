using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.BookeoImports;

/// <summary>
/// The import ledger: one row per Bookeo booking number ever applied, holding what was imported
/// (so a re-upload can be diffed into new / changed / cancelled / unchanged) and which trip its
/// passengers went onto. <see cref="BookingNumber"/> is unique per tenant — the DB-atomic
/// idempotency key. Totals are tax-inclusive gross, paid and due only; no tax figure exists here.
/// <para>
/// A booking that is in the ledger but missing from a later file is left alone: Bookeo reports
/// are date-filtered, so absence is never a cancellation.
/// </para>
/// </summary>
public sealed class BookeoBooking : Entity, ITenantScoped
{
    private BookeoBooking()
    {
        BookingNumber = null!;
        ProductCode = null!;
        ProductName = null!;
        BookeoStatus = null!;
        CustomerName = null!;
        ContentHash = null!;
    }

    public Guid TenantId { get; private set; }
    public string BookingNumber { get; private set; }
    public string ProductCode { get; private set; }
    public string ProductName { get; private set; }
    public string? Destination { get; private set; }
    public DateOnly ServiceDate { get; private set; }
    public TimeOnly WindowStart { get; private set; }
    public TimeOnly? WindowEnd { get; private set; }
    public string BookeoStatus { get; private set; }
    public int Participants { get; private set; }
    public List<BookeoBookingPassenger> Passengers { get; private set; } = [];
    public string CustomerName { get; private set; }
    public string? CustomerEmail { get; private set; }
    public string? CustomerPhone { get; private set; }
    public decimal TotalGrossCad { get; private set; }
    public decimal TotalPaidCad { get; private set; }
    public decimal TotalDueCad { get; private set; }
    public string? UnitText { get; private set; }

    /// <summary>The trip carrying this booking's manifest rows — null once cancelled.</summary>
    public Guid? TripId { get; private set; }

    public string ContentHash { get; private set; }
    public DateTimeOffset FirstImportedAtUtc { get; private set; }
    public DateTimeOffset LastImportedAtUtc { get; private set; }
    public Guid LastBatchId { get; private set; }

    public static BookeoBooking Record(
        Guid tenantId, BookeoBookingSnapshot snapshot, Guid? tripId, Guid batchId, DateTimeOffset now)
    {
        var booking = new BookeoBooking
        {
            TenantId = tenantId,
            BookingNumber = snapshot.BookingNumber,
            FirstImportedAtUtc = now,
        };
        booking.Apply(snapshot, tripId, batchId, now);
        return booking;
    }

    /// <summary>Replaces the imported content (a changed or cancelled booking) and re-points the trip.</summary>
    public void Apply(BookeoBookingSnapshot snapshot, Guid? tripId, Guid batchId, DateTimeOffset now)
    {
        ProductCode = snapshot.ProductCode;
        ProductName = snapshot.ProductName;
        Destination = snapshot.Destination;
        ServiceDate = snapshot.ServiceDate;
        WindowStart = snapshot.WindowStart;
        WindowEnd = snapshot.WindowEnd;
        BookeoStatus = snapshot.BookeoStatus;
        Participants = snapshot.Participants;
        Passengers = [.. snapshot.Passengers];
        CustomerName = snapshot.CustomerName;
        CustomerEmail = snapshot.CustomerEmail;
        CustomerPhone = snapshot.CustomerPhone;
        TotalGrossCad = snapshot.TotalGrossCad;
        TotalPaidCad = snapshot.TotalPaidCad;
        TotalDueCad = snapshot.TotalDueCad;
        UnitText = snapshot.UnitText;
        ContentHash = snapshot.ContentHash;
        TripId = tripId;
        Touch(batchId, now);
    }

    /// <summary>An unchanged booking seen again: only the "last seen" stamps move.</summary>
    public void Touch(Guid batchId, DateTimeOffset now)
    {
        LastImportedAtUtc = now;
        LastBatchId = batchId;
    }

    /// <summary>The ledger's content in the same shape a freshly parsed row produces.</summary>
    public BookeoBookingSnapshot ToSnapshot() => new(
        BookingNumber, ProductCode, ProductName, Destination, ServiceDate, WindowStart, WindowEnd,
        BookeoStatus, Participants, Passengers, CustomerName, CustomerEmail, CustomerPhone,
        TotalGrossCad, TotalPaidCad, TotalDueCad, UnitText, ContentHash);
}

/// <summary>One imported passenger as Bookeo listed them (jsonb). Stops and fares live on the manifest row.</summary>
public sealed record BookeoBookingPassenger
{
    public required string Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Category { get; init; }
}

/// <summary>A booking's importable content — what the ledger stores and what a parsed row is diffed as.</summary>
public sealed record BookeoBookingSnapshot(
    string BookingNumber,
    string ProductCode,
    string ProductName,
    string? Destination,
    DateOnly ServiceDate,
    TimeOnly WindowStart,
    TimeOnly? WindowEnd,
    string BookeoStatus,
    int Participants,
    IReadOnlyList<BookeoBookingPassenger> Passengers,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    decimal TotalGrossCad,
    decimal TotalPaidCad,
    decimal TotalDueCad,
    string? UnitText,
    string ContentHash);
