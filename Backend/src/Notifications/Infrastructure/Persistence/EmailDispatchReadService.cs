using Microsoft.EntityFrameworkCore;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>
/// Read side — queries notifications.rm_email_dispatches and maps to the public contract
/// (the recipients jsonb materializes with the read model).
/// </summary>
internal sealed class EmailDispatchReadService(NotificationsDbContext context) : IEmailDispatchReadService
{
    public async Task<IReadOnlyList<EmailDispatchResponse>> GetForTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var dispatches = await context.EmailDispatchReadModels
            .AsNoTracking()
            .Where(d => d.TripId == tripId)
            .OrderByDescending(d => d.SentAtUtc)
            .ToListAsync(cancellationToken);

        return dispatches.Select(ToResponse).ToList();
    }

    private static EmailDispatchResponse ToResponse(EmailDispatchReadModel dispatch) => new(
        dispatch.Id,
        dispatch.TripId,
        dispatch.TripNumber,
        dispatch.ManifestId,
        dispatch.TemplateId,
        dispatch.TemplateName,
        dispatch.ServiceType,
        dispatch.Status,
        dispatch.SentAtUtc,
        dispatch.Recipients
            .Select(r => new RecipientResult(
                r.Email, r.PassengerName, r.Status.ToString(), r.ErrorCode, r.ErrorMessage, r.PostmarkMessageId))
            .ToList());
}
