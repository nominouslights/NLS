using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Domain.Authority;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Vehicles;

/// <summary>
/// Sets (true/false) or clears (null) the Compliance-only regulated-vehicle override on a vehicle
/// in the replica — owner question Q1's per-vehicle escape hatch from the seating rule.
/// </summary>
public sealed record SetVehicleRegulatedCommand(Guid TenantId, Guid VehicleId, bool? RegulatedOverride, Guid? ActorId) : ICommand;

/// <summary>Every vehicle Compliance knows, with its derived regulated status.</summary>
public sealed record ListComplianceVehiclesQuery(Guid TenantId, DateOnly AsOf) : IQuery<IReadOnlyList<ComplianceVehicleResponse>>;

/// <summary>
/// A vehicle as Compliance sees it. <c>Regulated</c> = <c>RegulatedOverride</c> ?? seating ≥
/// <c>RegulatedMinSeatingCapacity</c> (the rule set's <c>authority.regulatedMinSeatingCapacity</c>);
/// <c>CountsForReadiness</c> is false for Retired, Sold and Recycled vehicles.
/// </summary>
public sealed record ComplianceVehicleResponse(
    Guid VehicleId,
    string UnitNumber,
    string Status,
    int SeatingCapacity,
    string RequiredLicenceClass,
    bool? RegulatedOverride,
    bool Regulated,
    int RegulatedMinSeatingCapacity,
    bool CountsForReadiness,
    DateTimeOffset UpdatedAtUtc)
{
    public static ComplianceVehicleResponse From(VehicleLookup v, int minSeating) => new(
        v.VehicleId, v.UnitNumber, v.Status, v.SeatingCapacity, v.RequiredLicenceClass, v.RegulatedOverride,
        v.IsRegulated(minSeating), minSeating, v.CountsForReadiness, v.UpdatedAtUtc);
}

/// <summary>
/// Handles <see cref="SetVehicleRegulatedCommand"/> — HTA s. 1(1) "bus" / "regulated vehicle".
/// The override lives on the replica row (the upsert never touches it), so it survives every
/// later vehicle-changed event.
/// </summary>
public sealed class SetVehicleRegulatedCommandHandler(IVehicleLookupRepository vehicles)
    : ICommandHandler<SetVehicleRegulatedCommand>
{
    public async Task<Result> Handle(SetVehicleRegulatedCommand command, CancellationToken cancellationToken) =>
        await vehicles.SetRegulatedOverrideAsync(command.VehicleId, command.RegulatedOverride, cancellationToken)
            ? Result.Success()
            : Result.Failure(ComplianceVehicleErrors.NotFound);
}

/// <summary>Handles <see cref="ListComplianceVehiclesQuery"/>, deriving regulated status from the rule set in force.</summary>
public sealed class ListComplianceVehiclesQueryHandler(IVehicleLookupRepository vehicles, ActiveRuleSetResolver rules)
    : IQueryHandler<ListComplianceVehiclesQuery, IReadOnlyList<ComplianceVehicleResponse>>
{
    public async Task<Result<IReadOnlyList<ComplianceVehicleResponse>>> Handle(ListComplianceVehiclesQuery query, CancellationToken cancellationToken)
    {
        var active = await rules.ResolveAsync(query.AsOf, cancellationToken);
        var minSeating = active.Document.EffectiveAuthority.RegulatedMinSeatingCapacity;
        var rows = await vehicles.ListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ComplianceVehicleResponse>>(
            rows.Select(v => ComplianceVehicleResponse.From(v, minSeating)).ToList());
    }
}
