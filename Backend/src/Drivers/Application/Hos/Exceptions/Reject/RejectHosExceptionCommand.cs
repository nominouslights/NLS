using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Reject;

/// <summary>A compliance officer refuses an exception; it stops applying at once. Idempotent.</summary>
public sealed record RejectHosExceptionCommand(Guid TenantId, Guid DriverId, Guid ExceptionId, string? Note) : ICommand<HosExceptionResponse>;
