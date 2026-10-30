using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Acknowledge;

/// <summary>Dispatch or a compliance officer marks a declared exception seen. Idempotent.</summary>
public sealed record AcknowledgeHosExceptionCommand(Guid TenantId, Guid DriverId, Guid ExceptionId) : ICommand<HosExceptionResponse>;
