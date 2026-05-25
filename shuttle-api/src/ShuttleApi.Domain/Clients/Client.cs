using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Clients;

public sealed class Client : AggregateRoot<Guid>
{
    public string BusinessName { get; private set; } = string.Empty;
    public ServiceType ServiceType { get; private set; }
    public string PrimaryContactName { get; private set; } = string.Empty;
    public string PrimaryContactTitle { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string StreetAddress { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Province { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string? GstHstNumber { get; private set; }
    public string PreferredPaymentMethod { get; private set; } = string.Empty;
    public int NetPaymentTerms { get; private set; }
    public decimal OutstandingBalance { get; private set; }
    public string? ComplianceNotes { get; private set; }
    public bool IsMinesite { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? Industry { get; private set; }
    public string? ProjectSite { get; private set; }

    private Client() { }

    public static Client Create(
        string businessName,
        ServiceType serviceType,
        string primaryContactName,
        string primaryContactTitle,
        string phone,
        string email,
        string streetAddress,
        string city,
        string province,
        string postalCode,
        string? gstHstNumber,
        string preferredPaymentMethod,
        int netPaymentTerms,
        string? complianceNotes,
        bool isMinesite,
        string? industry,
        string? projectSite)
    {
        return new Client
        {
            Id = Guid.NewGuid(),
            BusinessName = businessName,
            ServiceType = serviceType,
            PrimaryContactName = primaryContactName,
            PrimaryContactTitle = primaryContactTitle,
            Phone = phone,
            Email = email,
            StreetAddress = streetAddress,
            City = city,
            Province = province,
            PostalCode = postalCode,
            GstHstNumber = gstHstNumber,
            PreferredPaymentMethod = preferredPaymentMethod,
            NetPaymentTerms = netPaymentTerms,
            OutstandingBalance = 0m,
            ComplianceNotes = complianceNotes,
            IsMinesite = isMinesite,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Industry = industry,
            ProjectSite = projectSite
        };
    }

    public void Update(
        string businessName,
        ServiceType serviceType,
        string primaryContactName,
        string primaryContactTitle,
        string phone,
        string email,
        string streetAddress,
        string city,
        string province,
        string postalCode,
        string? gstHstNumber,
        string preferredPaymentMethod,
        int netPaymentTerms,
        string? complianceNotes,
        bool isMinesite,
        string? industry,
        string? projectSite)
    {
        BusinessName = businessName;
        ServiceType = serviceType;
        PrimaryContactName = primaryContactName;
        PrimaryContactTitle = primaryContactTitle;
        Phone = phone;
        Email = email;
        StreetAddress = streetAddress;
        City = city;
        Province = province;
        PostalCode = postalCode;
        GstHstNumber = gstHstNumber;
        PreferredPaymentMethod = preferredPaymentMethod;
        NetPaymentTerms = netPaymentTerms;
        ComplianceNotes = complianceNotes;
        IsMinesite = isMinesite;
        Industry = industry;
        ProjectSite = projectSite;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public void SetOutstandingBalance(decimal balance) => OutstandingBalance = balance;
}
