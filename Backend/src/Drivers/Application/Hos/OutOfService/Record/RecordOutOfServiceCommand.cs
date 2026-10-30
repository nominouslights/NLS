using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.OutOfService.Record;

/// <summary>
/// A compliance officer records that enforcement declared the driver out of service at
/// <paramref name="DeclaredAtUtc"/>. <paramref name="UntilUtc"/> null means the 72-hour default
/// unless <paramref name="OpenEnded"/> is true (until lifted). Recording opens a Severe
/// <c>OutOfServiceDeclared</c> finding in the same transaction. At most one active declaration
/// per driver.
/// </summary>
public sealed record RecordOutOfServiceCommand(
    Guid TenantId,
    Guid DriverId,
    DateTimeOffset DeclaredAtUtc,
    DateTimeOffset? UntilUtc,
    bool OpenEnded,
    string? Reason,
    string? Evidence) : ICommand<OutOfServiceRecordResponse>;
