using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.SetAnchorDate;

/// <summary>Records an anchor date learned after registration (ClaimClosed, VehicleLeftControl, EmploymentEnd …) and recomputes retention.</summary>
public sealed record SetAnchorDateCommand(Guid TenantId, Guid RecordId, AnchorKind Anchor, DateOnly Date, Guid? ActorId) : ICommand;
