using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Application.Inspections.GetDefects;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Wiring only. The five-step derivation and the ordering run in the EF read service against a
/// real read model (covered by the Fleet integration tests); here we prove the handler forwards
/// both narrowing arguments unchanged — in particular that <c>IncludeResolved</c> is not
/// defaulted or inverted on the way down, since that flag decides whether a cleared safety
/// record shows up at all.
///
/// The fake is a one-method double because <see cref="IVehicleDefectReadService"/> is its own
/// interface: nothing in this file has to know about inspection listing.
/// </summary>
public class GetVehicleDefectsQueryHandlerTests
{
    private sealed class CapturingDefectReadService : IVehicleDefectReadService
    {
        public Guid RequestedVehicleId { get; private set; }
        public bool RequestedIncludeResolved { get; private set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<VehicleDefectResponse>> GetDefectsForVehicleAsync(
            Guid vehicleId,
            bool includeResolved,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            RequestedVehicleId = vehicleId;
            RequestedIncludeResolved = includeResolved;
            return Task.FromResult<IReadOnlyList<VehicleDefectResponse>>([]);
        }
    }

    [Fact]
    public async Task The_handler_forwards_the_vehicle_id_and_include_resolved_flag()
    {
        var readService = new CapturingDefectReadService();
        var handler = new GetVehicleDefectsQueryHandler(readService);
        var vehicleId = Guid.NewGuid();

        var result = await handler.Handle(
            new GetVehicleDefectsQuery(TestVehicles.TenantId, vehicleId, IncludeResolved: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, readService.CallCount);
        Assert.Equal(vehicleId, readService.RequestedVehicleId);
        Assert.True(readService.RequestedIncludeResolved);
    }

    [Fact]
    public async Task The_default_query_asks_for_open_defects_only()
    {
        var readService = new CapturingDefectReadService();
        var handler = new GetVehicleDefectsQueryHandler(readService);

        var result = await handler.Handle(
            new GetVehicleDefectsQuery(TestVehicles.TenantId, Guid.NewGuid(), IncludeResolved: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.False(readService.RequestedIncludeResolved);
    }
}
