using NorthernLink.Shared.Messaging;

namespace NorthernLink.Budgeting.Application.Qbo.Complete;

/// <summary>
/// Finishes the OAuth round trip with the three values Intuit put on the callback URL. The
/// console's callback page posts them here; the API is never the redirect target, because it is
/// not reachable from the public internet. <paramref name="UserId"/> comes from the signed token
/// and must be the user who started the flow.
/// </summary>
public sealed record CompleteQboConnectionCommand(
    Guid TenantId,
    Guid? UserId,
    string? Code,
    string? State,
    string? RealmId) : ICommand;
