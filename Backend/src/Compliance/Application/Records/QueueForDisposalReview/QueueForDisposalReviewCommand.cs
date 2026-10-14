using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Records.QueueForDisposalReview;

/// <summary>
/// The nightly sweep for one tenant: recompute retention for every retained record and move the
/// ones whose retain-until is on or before <paramref name="AsOf"/> — and that carry no hold — to
/// ReviewForDisposal. Returns how many moved. Dispatched by <c>RetentionWorker</c> under the
/// tenant's ambient scope, and by the manual sweep endpoint.
/// </summary>
public sealed record QueueForDisposalReviewCommand(Guid TenantId, DateOnly AsOf) : ICommand<int>;
