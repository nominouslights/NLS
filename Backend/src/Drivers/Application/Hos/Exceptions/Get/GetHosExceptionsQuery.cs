using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.Exceptions.Get;

/// <summary>The driver's exceptions (every status) whose span touches [<paramref name="From"/>, <paramref name="To"/>] (America/Winnipeg days), oldest first.</summary>
public sealed record GetHosExceptionsQuery(Guid TenantId, Guid DriverId, DateOnly From, DateOnly To)
    : IQuery<IReadOnlyList<HosExceptionResponse>>;
