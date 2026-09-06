using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Bookings;

/// <summary>
/// Where a booking is picked up or dropped off: an optional catalog stop reference
/// (<see cref="StopId"/> — a Trips stop, referenced by id only, never joined), the stop's
/// name snapshot, and/or a free-text address detail. Rules: a stop reference requires its
/// name snapshot, and at least one of stop name / address detail must be present. This
/// shape serves community-to-community today and door-to-door later without a migration
/// (address detail alone is already valid).
/// </summary>
public sealed record BookingLocation
{
    private BookingLocation(Guid? stopId, string? stopName, string? addressDetail)
    {
        StopId = stopId;
        StopName = stopName;
        AddressDetail = addressDetail;
    }

    public Guid? StopId { get; }
    public string? StopName { get; }
    public string? AddressDetail { get; }

    public static Result<BookingLocation> Create(Guid? stopId, string? stopName, string? addressDetail)
    {
        var name = Clean(stopName);
        var detail = Clean(addressDetail);

        if (stopId is not null && name is null)
        {
            return Result.Failure<BookingLocation>(BookingErrors.LocationStopNameRequired);
        }

        if (name is null && detail is null)
        {
            return Result.Failure<BookingLocation>(BookingErrors.LocationRequired);
        }

        return Result.Success(new BookingLocation(stopId, name, detail));
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
