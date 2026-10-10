using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Qbo.Authorize;

/// <summary>
/// Starts the OAuth round trip: records a single-use state bound to this tenant and user, and
/// returns Intuit's authorize URL for the browser to visit. <paramref name="UserId"/> comes from
/// the signed token; a request without one is refused, because the callback has to be matched
/// back to the same person.
/// </summary>
public sealed record StartQboAuthorizationCommand(Guid TenantId, Guid? UserId) : ICommand<string>;
