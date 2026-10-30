using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.OutOfService.Lift;

/// <summary>A compliance officer lifts the declaration. Early (before its end, or when open-ended) requires <paramref name="Reason"/>. Idempotent.</summary>
public sealed record LiftOutOfServiceCommand(Guid TenantId, Guid DriverId, Guid RecordId, string? Reason) : ICommand<OutOfServiceRecordResponse>;
