using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.EldMalfunctions.Resolve;

/// <summary>A compliance officer confirms the ELD is repaired. Idempotent.</summary>
public sealed record ResolveEldMalfunctionCommand(Guid TenantId, Guid DriverId, Guid ReportId, string? Note) : ICommand<EldMalfunctionResponse>;
