using NorthernLink.Fleet.Application.Inspections.AcknowledgeCarrier;
using NorthernLink.Fleet.Domain.Inspections;
using Xunit;

namespace NorthernLink.Fleet.Tests;

/// <summary>
/// Handler wiring for the NL-PTI-01 carrier acknowledgement: the aggregate owns the rules, so
/// this proves only that the inspection is loaded, the failure paths save nothing, and the
/// timestamp is stamped server-side. The one behaviour that is the handler's own is the
/// DELIBERATE ABSENCE of the "Dispatch" fallback that <c>ResolveDefect</c> and <c>Enter</c>
/// apply — a blank name must fail here, not be attributed to nobody in particular.
/// </summary>
public class AcknowledgeInspectionCarrierCommandHandlerTests
{
    private static AcknowledgeInspectionCarrierCommand Command(
        Guid inspectionId, string? acknowledgedBy = "R. Beardy") =>
        new(
            TestVehicles.TenantId,
            inspectionId,
            acknowledgedBy,
            Note: "Unit parked pending repair");

    private static (AcknowledgeInspectionCarrierCommandHandler Handler, InMemoryVehicleInspectionRepository Repository)
        Setup(params VehicleInspection[] stored)
    {
        var repository = new InMemoryVehicleInspectionRepository();
        foreach (var inspection in stored)
        {
            repository.Add(inspection);
        }

        return (new AcknowledgeInspectionCarrierCommandHandler(repository), repository);
    }

    private static VehicleInspection FailedInspection() =>
        TestInspections.PreTrip(defects: [TestInspections.Defect(InspectionDefectSeverity.Major)]);

    [Fact]
    public async Task An_unknown_inspection_fails_with_not_found_and_saves_nothing()
    {
        var (handler, repository) = Setup();

        var result = await handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.NotFound, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Acknowledging_stamps_the_inspection_and_saves_once()
    {
        var inspection = FailedInspection();
        var (handler, repository) = Setup(inspection);
        var before = DateTimeOffset.UtcNow;

        var result = await handler.Handle(Command(inspection.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal("R. Beardy", inspection.CarrierAcknowledgedBy);
        Assert.Equal("Unit parked pending repair", inspection.CarrierAcknowledgementNote);
        // Stamped server-side, never taken from the request body.
        Assert.InRange(inspection.CarrierAcknowledgedAtUtc!.Value, before, DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_missing_name_is_rejected_rather_than_attributed_to_dispatch(string? acknowledgedBy)
    {
        var inspection = FailedInspection();
        var (handler, repository) = Setup(inspection);

        var result = await handler.Handle(
            Command(inspection.Id, acknowledgedBy), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.CarrierAcknowledgerRequired, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount);
        Assert.Null(inspection.CarrierAcknowledgedAtUtc);
    }

    [Fact]
    public async Task A_clean_inspection_is_rejected_and_saves_nothing()
    {
        var inspection = TestInspections.PreTrip();
        var (handler, repository) = Setup(inspection);

        var result = await handler.Handle(Command(inspection.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(InspectionErrors.CarrierAcknowledgementNotRequired, result.Error);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task A_second_acknowledgement_is_rejected_and_saves_nothing()
    {
        var inspection = FailedInspection();
        var (handler, repository) = Setup(inspection);

        Assert.True((await handler.Handle(Command(inspection.Id), CancellationToken.None)).IsSuccess);

        var second = await handler.Handle(
            Command(inspection.Id, acknowledgedBy: "Someone Else"), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal(InspectionErrors.CarrierAlreadyAcknowledged, second.Error);
        Assert.Equal(1, repository.SaveChangesCallCount);
        Assert.Equal("R. Beardy", inspection.CarrierAcknowledgedBy);
    }
}
