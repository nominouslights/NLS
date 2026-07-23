using NorthernLink.Clients.Domain.ClientContacts.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.ClientContacts;

/// <summary>
/// A contact person at a client organization — a CRM record of a person with a role, email,
/// and phone. Not a platform user account (that's Identity's concern) — a contact may or may
/// not correspond to a user who can log in. ClientContact is reference data with no lifecycle
/// of its own: create only (no update/delete in this scope). Create raises a module-internal
/// domain event (never mapped to an integration event) so the write lands in
/// <c>event_journal</c> for the audit trail and any future read-model projection.
/// </summary>
public sealed class ClientContact : AggregateRoot, ITenantScoped
{
    private ClientContact()
    {
        // EF Core materialization only.
        Name = null!;
        Title = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; }
    public string Title { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<ClientContact> Create(
        Guid tenantId,
        Guid clientId,
        string name,
        string title,
        string? email,
        string? phone,
        string? notes,
        bool isPrimary)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ClientContact>(ClientContactErrors.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure<ClientContact>(ClientContactErrors.TitleRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var contact = new ClientContact
        {
            TenantId = tenantId,
            ClientId = clientId,
            Name = name.Trim(),
            Title = title.Trim(),
            Email = Clean(email),
            Phone = Clean(phone),
            Notes = Clean(notes),
            IsPrimary = isPrimary,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        contact.Raise(new ClientContactCreatedDomainEvent(contact.Id, clientId, tenantId));
        return Result.Success(contact);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
