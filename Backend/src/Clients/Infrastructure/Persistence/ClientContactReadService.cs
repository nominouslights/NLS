using Microsoft.EntityFrameworkCore;
using NorthernLink.Clients.Application.Abstractions;
using NorthernLink.Clients.Application.ClientContacts;
using NorthernLink.Clients.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Clients.Infrastructure.Persistence;

/// <summary>Read side — queries clients.rm_client_contacts and maps to the public contract.</summary>
internal sealed class ClientContactReadService(ClientsDbContext context) : IClientContactReadService
{
    public async Task<IReadOnlyList<ClientContactResponse>> GetForClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var contacts = await context.ClientContactReadModels
            .AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return contacts.Select(ToResponse).ToList();
    }

    private static ClientContactResponse ToResponse(ClientContactReadModel contact) => new(
        contact.Id,
        contact.ClientId,
        contact.Name,
        contact.Title,
        contact.Email,
        contact.Phone,
        contact.Notes,
        contact.IsPrimary,
        contact.ReceivesEmailReports,
        contact.CreatedAtUtc,
        contact.UpdatedAtUtc);
}
