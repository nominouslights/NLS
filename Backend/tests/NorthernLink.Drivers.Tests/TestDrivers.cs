using NorthernLink.Drivers.Domain.Drivers;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Tests;

/// <summary>Canonical valid inputs for Driver tests — override only what a test cares about.</summary>
public static class TestDrivers
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static Result<Driver> Register(
        string name = "J. Spence",
        string? phone = "204-555-0147",
        string licenceClass = "Class 2",
        DateOnly? licenceExpiry = null,
        string source = "Northern Link",
        bool hasWorkPermit = false) =>
        Driver.Register(
            TenantId,
            name,
            phone,
            licenceClass,
            licenceExpiry ?? new DateOnly(2027, 3, 31),
            source,
            hasWorkPermit);
}
