using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.EldMalfunctions.Report;

/// <summary>The driver (own record) or dispatch reports that the bus's ELD stopped recording at <paramref name="NoticedAtUtc"/>.</summary>
public sealed record ReportEldMalfunctionCommand(
    Guid TenantId,
    Guid DriverId,
    Guid? VehicleId,
    string? Code,
    DateTimeOffset NoticedAtUtc,
    string? Description) : ICommand<EldMalfunctionResponse>;
