using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Drivers.GetByUser;

/// <summary>
/// The roster row belonging to an Identity user — what <c>GET /api/drivers/me</c> answers.
/// <paramref name="UserId"/> comes from the access token's <c>sub</c>, never from the request, so
/// a caller cannot ask for somebody else's "me".
/// </summary>
public sealed record GetDriverByUserQuery(Guid TenantId, Guid UserId) : IQuery<DriverResponse>;
