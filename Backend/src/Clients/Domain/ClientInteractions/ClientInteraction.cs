using NorthernLink.Clients.Domain.ClientInteractions.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.ClientInteractions;

/// <summary>
/// A logged interaction with a client — a call, meeting, email, site visit, or other touchpoint,
/// optionally carrying a follow-up reminder. Client-scoped reference data with no lifecycle of its
/// own: create and hard-delete only (no update in this scope). Create raises a module-internal
/// domain event (never mapped to an integration event) so the write lands in <c>event_journal</c>
/// for the audit trail and the read-model projection.
/// </summary>
public sealed class ClientInteraction : AggregateRoot, ITenantScoped
{
    private ClientInteraction()
    {
        // EF Core materialization only.
        Summary = null!;
        ParticipantContactIds = [];
    }

    public Guid TenantId { get; private set; }
    public Guid ClientId { get; private set; }
    public InteractionType Type { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string Summary { get; private set; }
    public List<Guid> ParticipantContactIds { get; private set; }
    public DateOnly? FollowUpDate { get; private set; }
    public string? FollowUpNote { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<ClientInteraction> Create(
        Guid tenantId,
        Guid clientId,
        InteractionType type,
        DateOnly occurredOn,
        string summary,
        IEnumerable<Guid>? participantContactIds,
        DateOnly? followUpDate,
        string? followUpNote)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return Result.Failure<ClientInteraction>(ClientInteractionErrors.SummaryRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var interaction = new ClientInteraction
        {
            TenantId = tenantId,
            ClientId = clientId,
            Type = type,
            OccurredOn = occurredOn,
            Summary = summary.Trim(),
            ParticipantContactIds = participantContactIds?.ToList() ?? [],
            FollowUpDate = followUpDate,
            FollowUpNote = Clean(followUpNote),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        interaction.Raise(new ClientInteractionCreatedDomainEvent(interaction.Id, clientId, tenantId));
        return Result.Success(interaction);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
