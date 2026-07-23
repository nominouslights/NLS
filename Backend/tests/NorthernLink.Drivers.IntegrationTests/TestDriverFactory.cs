using NorthernLink.Drivers.Domain.Clearances;
using NorthernLink.Drivers.Domain.Credentials;
using NorthernLink.Drivers.Domain.Drivers;
using Xunit;

namespace NorthernLink.Drivers.IntegrationTests;

/// <summary>
/// Builds valid drivers/credentials/clearances for the shared database — every test runs
/// against the same container, so names are made unique with a counter.
/// </summary>
internal static class TestDriverFactory
{
    private static int _counter;

    public static Driver CreateDriver(Guid tenantId)
    {
        var sequence = Interlocked.Increment(ref _counter);
        var result = Driver.Register(
            tenantId,
            name: $"Test Driver {sequence:D4}",
            phone: "204-555-0147",
            licenceClass: "Class 2",
            licenceExpiry: new DateOnly(2028, 3, 31),
            source: "Northern Link",
            hasWorkPermit: false);

        Assert.True(result.IsSuccess, $"Test driver registration failed: {result.Error.Code}");
        return result.Value;
    }

    public static DriverCredential CreateCredential(
        Guid tenantId,
        Guid driverId,
        DateOnly? expiry = null)
    {
        var result = DriverCredential.Add(
            tenantId,
            driverId,
            type: "First Aid",
            label: "Standard First Aid & CPR-C",
            issued: new DateOnly(2026, 1, 15),
            expiry: expiry,
            optional: false,
            note: null);

        Assert.True(result.IsSuccess, $"Test credential add failed: {result.Error.Code}");
        return result.Value;
    }

    public static DriverClearance CreateClearance(Guid tenantId, Guid driverId)
    {
        var result = DriverClearance.Grant(
            tenantId,
            driverId,
            title: "Site Induction",
            clientName: "Alamos Gold — Lynn Lake",
            expiry: new DateOnly(2027, 5, 1));

        Assert.True(result.IsSuccess, $"Test clearance grant failed: {result.Error.Code}");
        return result.Value;
    }
}
