using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Inspections;
using NorthernLink.Fleet.Application.Inspections.GetInspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Wiring for the trip-number list filter. The actual <c>WHERE TripNumber == @trip</c> runs in
/// the EF read service against the real read model (covered by the Fleet integration tests); here
/// we prove the query carries the trip number and the handler forwards both narrowing arguments
/// down to the read service unchanged — the fix for the bug where every trip showed all
/// inspections started with the query/handler never passing the trip number along.
/// </summary>
public class GetVehicleInspectionsQueryHandlerTests
{
    private sealed class CapturingReadService : IVehicleInspectionReadService
    {
        public string? RequestedUnit { get; private set; }
        public string? RequestedTripNumber { get; private set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<VehicleInspectionResponse>> GetInspectionsAsync(
            string? unit = null,
            string? tripNumber = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            RequestedUnit = unit;
            RequestedTripNumber = tripNumber;
            return Task.FromResult<IReadOnlyList<VehicleInspectionResponse>>([]);
        }
    }

    [Fact]
    public async Task The_handler_forwards_the_trip_number_and_unit_to_the_read_service()
    {
        var readService = new CapturingReadService();
        var handler = new GetVehicleInspectionsQueryHandler(readService);

        var result = await handler.Handle(
            new GetVehicleInspectionsQuery(TestVehicles.TenantId, Unit: null, TripNumber: "TR-4818"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, readService.CallCount);
        Assert.Equal("TR-4818", readService.RequestedTripNumber);
        Assert.Null(readService.RequestedUnit);
    }

    [Fact]
    public async Task A_query_with_no_narrowing_passes_nulls_through()
    {
        var readService = new CapturingReadService();
        var handler = new GetVehicleInspectionsQueryHandler(readService);

        await handler.Handle(new GetVehicleInspectionsQuery(TestVehicles.TenantId), CancellationToken.None);

        Assert.Null(readService.RequestedUnit);
        Assert.Null(readService.RequestedTripNumber);
    }
}
