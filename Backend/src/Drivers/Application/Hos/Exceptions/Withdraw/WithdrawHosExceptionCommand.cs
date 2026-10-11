using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Withdraw;

/// <summary>
/// Takes an exception back. The driver (own record — the endpoint checks) or dispatch may
/// withdraw while it is still Declared; a compliance officer may withdraw at any applying
/// status. Idempotent.
/// </summary>
public sealed record WithdrawHosExceptionCommand(Guid TenantId, Guid DriverId, Guid ExceptionId, string? Note) : ICommand<HosExceptionResponse>;
