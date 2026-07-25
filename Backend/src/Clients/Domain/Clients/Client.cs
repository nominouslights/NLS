using NorthernLink.Shared.Kernel;
using NorthernLink.Clients.Domain.Clients.Events;

namespace NorthernLink.Clients.Domain.Clients;

/// <summary>
/// A client organization the dispatcher bills and schedules for — a mining contractor,
/// a community program, a charter customer, a cargo/grocery account; or a vendor/partner
/// the dispatcher contracts with (immutable discriminator via <see cref="Type"/>).
/// <see cref="Tag"/> is the CRM's free-form roster grouping ("Corporate", "Program", "Charter",
/// "Individuals", "Vendor / Partner"); <see cref="ServiceType"/> is the structured service
/// category other modules key on (required for Client, forbidden for VendorPartner).
/// Both Name and ServiceType changes are public contract (Trips keeps a <c>client_lookup</c>
/// replica), so create and update raise events the mapper turns into
/// <c>ClientChangedIntegrationEvent</c>.
/// </summary>
public sealed class Client : AggregateRoot, ITenantScoped
{
    private Client()
    {
        // EF Core materialization only.
        Name = null!;
        Tag = null!;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public ClientType Type { get; private set; }
    public ClientServiceType? ServiceType { get; private set; }
    public string Tag { get; private set; }
    public string? AgreementReference { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<Client> Create(
        Guid tenantId,
        string name,
        ClientType type,
        ClientServiceType? serviceType,
        string tag,
        string? agreementReference,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Client>(ClientErrors.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            return Result.Failure<Client>(ClientErrors.TagRequired);
        }

        if (ValidateServiceType(type, serviceType) is { } serviceTypeError)
        {
            return Result.Failure<Client>(serviceTypeError);
        }

        var now = DateTimeOffset.UtcNow;
        var client = new Client
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Type = type,
            ServiceType = serviceType,
            Tag = tag.Trim(),
            AgreementReference = Clean(agreementReference),
            Notes = Clean(notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        client.Raise(new ClientCreatedDomainEvent(client.Id, tenantId));
        return Result.Success(client);
    }

    public Result Update(string name, ClientServiceType? serviceType, string tag, string? agreementReference, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ClientErrors.NameRequired);
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            return Result.Failure(ClientErrors.TagRequired);
        }

        if (ValidateServiceType(Type, serviceType) is { } serviceTypeError)
        {
            return Result.Failure(serviceTypeError);
        }

        Name = name.Trim();
        ServiceType = serviceType;
        Tag = tag.Trim();
        AgreementReference = Clean(agreementReference);
        Notes = Clean(notes);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new ClientUpdatedDomainEvent(Id, TenantId));
        return Result.Success();
    }

    private static Error? ValidateServiceType(ClientType type, ClientServiceType? serviceType) =>
        type switch
        {
            ClientType.Client when serviceType is null => ClientErrors.ServiceTypeRequired,
            ClientType.VendorPartner when serviceType is not null => ClientErrors.ServiceTypeNotAllowed,
            _ => null,
        };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
