using Microsoft.Extensions.Logging.Abstractions;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Infrastructure.Persistence;

namespace NorthernLink.Fleet.IntegrationTests;

/// <summary>The real duplicate cascade over real repositories on one context, as DI composes it per request.</summary>
internal static class TestDuplicateCascade
{
    public static DuplicateDefectCascade Over(FleetDbContext context) =>
        new(
            new VehicleInspectionRepository(context),
            new VehicleRepository(context),
            NullLogger<DuplicateDefectCascade>.Instance);
}
