using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Domain.Riders;
using NorthernLink.Trips.Domain.Trips;

namespace NorthernLink.Trips.Tests;

/// <summary>Factory helpers to exercise Rider.Create with a valid baseline payload.</summary>
internal static class TestRiders
{
    public static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>The baseline first trip a rider is created from.</summary>
    public static readonly DateOnly FirstTripDate = new(2026, 7, 14);
    public const string FirstTripNumber = "TR-4818";

    /// <summary>
    /// A valid contract-crew rider from their first manifest appearance; override the
    /// single argument a test cares about.
    /// </summary>
    public static Result<Rider> Create(
        string name = "M. Beardy",
        TripServiceType serviceType = TripServiceType.ContractCrew,
        string? contact = "m.beardy@example.ca",
        DateOnly? tripDate = null,
        string tripNumber = FirstTripNumber) =>
        Rider.Create(
            TenantId,
            name,
            serviceType,
            contact,
            tripDate ?? FirstTripDate,
            tripNumber);
}
