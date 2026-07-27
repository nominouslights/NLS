using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Domain.Stops;

/// <summary>
/// A WGS84 geographic coordinate — latitude in [-90, 90], longitude in [-180, 180].
/// Supplied by the frontend's Google Places selection and persisted flattened onto the
/// stop row so the Live Map can consume it later. Value object: structural equality,
/// private construction via <see cref="Create"/> (same shape as <c>Vin</c>).
/// </summary>
public sealed record Coordinate
{
    private Coordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }
    public double Longitude { get; }

    public static Result<Coordinate> Create(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            return Result.Failure<Coordinate>(StopErrors.InvalidLatitude);
        }

        if (longitude is < -180 or > 180)
        {
            return Result.Failure<Coordinate>(StopErrors.InvalidLongitude);
        }

        return Result.Success(new Coordinate(latitude, longitude));
    }
}
