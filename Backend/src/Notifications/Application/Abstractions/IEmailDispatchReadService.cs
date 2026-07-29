using NorthernLink.Notifications.Application.Dispatches;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>Read side for dispatch-history queries — returns response DTOs directly (tenant-scoped).</summary>
public interface IEmailDispatchReadService
{
    /// <summary>Every dispatch recorded against a trip, newest first.</summary>
    Task<IReadOnlyList<EmailDispatchResponse>> GetForTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
