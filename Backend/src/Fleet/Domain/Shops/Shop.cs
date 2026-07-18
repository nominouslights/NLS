using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Shops;

/// <summary>
/// A repair shop or parts partner the dispatcher registers once and reuses on work orders
/// (fills §2 of the NL-WO-01 work order). Fleet-wide reference data — not vehicle-scoped.
/// <see cref="Number"/> (SHOP-01, …) is a human-readable business key; the GUID is the API key.
/// </summary>
public sealed class Shop : AggregateRoot, ITenantScoped
{
    private Shop()
    {
        // EF Core materialization only.
        Number = null!;
        Name = null!;
    }

    public Guid TenantId { get; private set; }
    public string Number { get; private set; }
    public string Name { get; private set; }
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public string? GstBusinessNo { get; private set; }
    public bool MpiAccredited { get; private set; }
    public string? InspectionStationNo { get; private set; }
    public bool SuppliesParts { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static Result<Shop> Register(
        Guid tenantId,
        string number,
        string name,
        string? contactName,
        string? phone,
        string? email,
        string? address,
        string? gstBusinessNo,
        bool mpiAccredited,
        string? inspectionStationNo,
        bool suppliesParts,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Shop>(ShopErrors.NameRequired);
        }

        var now = DateTimeOffset.UtcNow;
        var shop = new Shop
        {
            TenantId = tenantId,
            Number = number,
            Name = name.Trim(),
            ContactName = Clean(contactName),
            Phone = Clean(phone),
            Email = Clean(email),
            Address = Clean(address),
            GstBusinessNo = Clean(gstBusinessNo),
            MpiAccredited = mpiAccredited,
            InspectionStationNo = Clean(inspectionStationNo),
            SuppliesParts = suppliesParts,
            Notes = Clean(notes),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        return Result.Success(shop);
    }

    public Result UpdateDetails(
        string name,
        string? contactName,
        string? phone,
        string? email,
        string? address,
        string? gstBusinessNo,
        bool mpiAccredited,
        string? inspectionStationNo,
        bool suppliesParts,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ShopErrors.NameRequired);
        }

        Name = name.Trim();
        ContactName = Clean(contactName);
        Phone = Clean(phone);
        Email = Clean(email);
        Address = Clean(address);
        GstBusinessNo = Clean(gstBusinessNo);
        MpiAccredited = mpiAccredited;
        InspectionStationNo = Clean(inspectionStationNo);
        SuppliesParts = suppliesParts;
        Notes = Clean(notes);
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
