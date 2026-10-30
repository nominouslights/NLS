using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.BookeoImports.Commit;

/// <summary>
/// Applies a previewed Bookeo import. The plan is recomputed from the batch's stored rows and
/// the current database; it applies only when its hash equals <paramref name="PlanHash"/> (the
/// one the dispatcher looked at), otherwise 409 <c>PreviewStale</c>. A batch commits once
/// (409 <c>AlreadyCommitted</c>). Everything — trips, manifests, ledger, the batch stamp — is one
/// save. <paramref name="CommittedBy"/> comes from the signed token.
/// </summary>
public sealed record CommitBookeoImportCommand(
    Guid TenantId,
    Guid BatchId,
    string? PlanHash,
    string CommittedBy) : ICommand<BookeoImportCommitResult>;
