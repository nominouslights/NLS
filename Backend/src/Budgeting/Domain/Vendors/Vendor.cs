using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Allocations;
using NorthernLink.Budgeting.Domain.Codes;
using NorthernLink.Budgeting.Domain.Vendors.Events;

namespace NorthernLink.Budgeting.Domain.Vendors;

/// <summary>
/// One entry in the tenant's vendor register: a supplier the business pays, or — on revenue
/// items — a payer. <b>Tenant-wide, not per period</b>: unlike a budget code, a vendor is the same
/// counterparty in every period, so the register is one list the whole Budgeting console shares.
/// <para>
/// <b>The name is unique per tenant, ignoring case.</b> "Acme Fuel" and "ACME FUEL" are one
/// vendor to anyone reading a report, so <see cref="NormalizedName"/> (trim + upper-invariant) is
/// stored alongside the display <see cref="Name"/> and carries a unique (tenant_id,
/// normalized_name) index. The handlers check first so the common case is a readable 409; the
/// index is the double-submit backstop. Unlike a code string, a vendor's name <em>is</em>
/// renameable — nothing references it by string, only (from the next slice) by id.
/// </para>
/// <para>
/// <b>Retiring (<see cref="SetActive"/>) is the normal end of a vendor's life, not deletion</b>,
/// the <see cref="BudgetCode"/> rule: an inactive vendor stays listed and simply stops being
/// offered for new work. Hard delete is for a vendor created in error that nothing references,
/// and the application layer refuses it otherwise.
/// </para>
/// <para>
/// <b>No payment terms, no AP fields.</b> QuickBooks owns accounts payable; this register only
/// names the counterparty so budget items can point at one, and so a QuickBooks export can be
/// matched back to it (<see cref="QboDisplayName"/>).
/// </para>
/// </summary>
public sealed class Vendor : AggregateRoot, ITenantScoped
{
    /// <summary>Equal to the free-text vendor on a budget item, so every existing value fits the register.</summary>
    public const int NameMaxLength = BudgetAllocation.VendorMaxLength;

    public const int ContactNameMaxLength = 120;

    /// <summary>The practical maximum length of an email address (RFC 5321 path limit).</summary>
    public const int EmailMaxLength = 254;

    public const int PhoneMaxLength = 32;
    public const int AddressMaxLength = 500;
    public const int NotesMaxLength = 2000;
    public const int GstRegistrationNumberMaxLength = 32;

    /// <summary>QuickBooks Online's own limit on a vendor's DisplayName, so any QBO name fits.</summary>
    public const int QboDisplayNameMaxLength = 500;

    public const int DefaultBudgetCodeMaxLength = BudgetCode.CodeMaxLength;

    private Vendor()
    {
        // EF Core materialization only.
        Name = null!;
        NormalizedName = null!;
    }

    public Guid TenantId { get; private set; }

    /// <summary>The display name, trimmed, as the person typed it.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// <see cref="Name"/> normalized by <see cref="NormalizeName"/> — the uniqueness key. Never
    /// shown; kept in step with <see cref="Name"/> by every write.
    /// </summary>
    public string NormalizedName { get; private set; }

    public string? ContactName { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>
    /// The vendor's GST/HST registration number (business number + RT account), as printed on
    /// its invoices. <b>Reference data only</b>: trimmed and length-checked, nothing else.
    /// Nothing on the platform reads it to decide whether tax applies, to assert a rate, or to
    /// compute an amount — the platform never computes tax, and QuickBooks owns all of it
    /// (architecture non-negotiable 8). There is deliberately no "charges GST" flag and no rate
    /// beside it; adding either would be the platform computing tax by another name.
    /// </summary>
    public string? GstRegistrationNumber { get; private set; }

    /// <summary>
    /// The vendor's DisplayName in QuickBooks Online, when it differs from <see cref="Name"/>.
    /// Reserved as the match key for a future QuickBooks CSV import (a bill row names its vendor
    /// by this string). Free text — the platform has no QBO connection to check it against.
    /// </summary>
    public string? QboDisplayName { get; private set; }

    /// <summary>
    /// A budget code <em>string</em> (FLEET-MAINT) a new budget item for this vendor should start
    /// with. Normalized and format-checked exactly as a code string is
    /// (<see cref="BudgetCode.NormalizeCode"/> / <see cref="BudgetCode.HasValidCodeFormat"/>), but
    /// <b>not required to exist</b>: codes belong to a period, the string is their cross-period
    /// identity, and this only pre-fills a form — the item's own code is validated when the item
    /// is saved.
    /// </summary>
    public string? DefaultBudgetCode { get; private set; }

    /// <summary>False once retired — still listed and still resolvable, just not offered for new work.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Who created and last changed this vendor, from the access token's <c>sub</c> claim — never a request body.</summary>
    public Guid? CreatedBy { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Creates an active vendor. Uniqueness of the name is the handler's check.</summary>
    public static Result<Vendor> Create(Guid tenantId, VendorDetails details, Guid? actorId)
    {
        var normalized = Normalize(details);
        if (normalized.IsFailure)
        {
            return Result.Failure<Vendor>(normalized.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var vendor = new Vendor
        {
            TenantId = tenantId,
            IsActive = true,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Name = string.Empty,
            NormalizedName = string.Empty,
        };

        vendor.Apply(normalized.Value);
        vendor.Raise(new VendorCreatedDomainEvent(vendor.Id, tenantId, vendor.Name, actorId));
        return Result.Success(vendor);
    }

    /// <summary>
    /// Rewrites every editable field (a PUT). An edit that, once normalized, changes nothing is a
    /// silent success: no stamp, no event — so the audit journal records changes, not saves, and
    /// the save pipeline (which rejects a modified aggregate with no event) sees nothing modified.
    /// </summary>
    public Result Update(VendorDetails details, Guid? actorId)
    {
        var normalized = Normalize(details);
        if (normalized.IsFailure)
        {
            return normalized;
        }

        if (normalized.Value == Current())
        {
            return Result.Success();
        }

        Apply(normalized.Value);
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new VendorUpdatedDomainEvent(Id, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Retires or restores the vendor. A no-op when it is already in that state — no stamp, no
    /// event, the <see cref="BudgetCode.SetActive"/> rule — so a repeated click is not an error.
    /// </summary>
    public Result SetActive(bool active, Guid? actorId)
    {
        if (IsActive == active)
        {
            return Result.Success();
        }

        IsActive = active;
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new VendorActivationChangedDomainEvent(Id, active, actorId));
        return Result.Success();
    }

    /// <summary>
    /// The field rules on their own, with no aggregate. The update handler runs this before the
    /// name-uniqueness lookup so a malformed payload reports its validation error rather than a
    /// conflict it was never going to reach (the budget-code handlers' ordering rule).
    /// </summary>
    public static Result Validate(VendorDetails details)
    {
        var normalized = Normalize(details);
        return normalized.IsFailure ? Result.Failure(normalized.Error) : Result.Success();
    }

    /// <summary>
    /// Trim + upper-invariant: the case-insensitive uniqueness key. Public because the handlers
    /// look for a clash before (or without) touching an aggregate.
    /// </summary>
    public static string NormalizeName(string? name) =>
        name?.Trim().ToUpperInvariant() ?? string.Empty;

    /// <summary>The validated, trimmed field set one write would store.</summary>
    private readonly record struct NormalizedDetails(
        string Name,
        string? ContactName,
        string? Email,
        string? Phone,
        string? Address,
        string? Notes,
        string? GstRegistrationNumber,
        string? QboDisplayName,
        string? DefaultBudgetCode);

    private NormalizedDetails Current() => new(
        Name, ContactName, Email, Phone, Address, Notes, GstRegistrationNumber, QboDisplayName, DefaultBudgetCode);

    private void Apply(NormalizedDetails details)
    {
        Name = details.Name;
        NormalizedName = NormalizeName(details.Name);
        ContactName = details.ContactName;
        Email = details.Email;
        Phone = details.Phone;
        Address = details.Address;
        Notes = details.Notes;
        GstRegistrationNumber = details.GstRegistrationNumber;
        QboDisplayName = details.QboDisplayName;
        DefaultBudgetCode = details.DefaultBudgetCode;
    }

    private static Result<NormalizedDetails> Normalize(VendorDetails details)
    {
        var name = Trimmed(details.Name);
        if (name is null)
        {
            return Fail(VendorErrors.NameRequired);
        }

        if (name.Length > NameMaxLength)
        {
            return Fail(VendorErrors.NameTooLong);
        }

        var contactName = Trimmed(details.ContactName);
        if (contactName?.Length > ContactNameMaxLength)
        {
            return Fail(VendorErrors.ContactNameTooLong);
        }

        var email = Trimmed(details.Email);
        if (email?.Length > EmailMaxLength)
        {
            return Fail(VendorErrors.EmailTooLong);
        }

        if (email is not null && !LooksLikeEmail(email))
        {
            return Fail(VendorErrors.EmailInvalid);
        }

        var phone = Trimmed(details.Phone);
        if (phone?.Length > PhoneMaxLength)
        {
            return Fail(VendorErrors.PhoneTooLong);
        }

        var address = Trimmed(details.Address);
        if (address?.Length > AddressMaxLength)
        {
            return Fail(VendorErrors.AddressTooLong);
        }

        var notes = Trimmed(details.Notes);
        if (notes?.Length > NotesMaxLength)
        {
            return Fail(VendorErrors.NotesTooLong);
        }

        // Length only, by decision — see the property's doc comment. No checksum, no format.
        var gstRegistrationNumber = Trimmed(details.GstRegistrationNumber);
        if (gstRegistrationNumber?.Length > GstRegistrationNumberMaxLength)
        {
            return Fail(VendorErrors.GstRegistrationNumberTooLong);
        }

        var qboDisplayName = Trimmed(details.QboDisplayName);
        if (qboDisplayName?.Length > QboDisplayNameMaxLength)
        {
            return Fail(VendorErrors.QboDisplayNameTooLong);
        }

        string? defaultBudgetCode = null;
        if (!string.IsNullOrWhiteSpace(details.DefaultBudgetCode))
        {
            defaultBudgetCode = BudgetCode.NormalizeCode(details.DefaultBudgetCode);
            if (defaultBudgetCode.Length > DefaultBudgetCodeMaxLength)
            {
                return Fail(VendorErrors.DefaultBudgetCodeTooLong);
            }

            if (!BudgetCode.HasValidCodeFormat(defaultBudgetCode))
            {
                return Fail(VendorErrors.DefaultBudgetCodeInvalidFormat);
            }
        }

        return Result.Success(new NormalizedDetails(
            name, contactName, email, phone, address, notes, gstRegistrationNumber, qboDisplayName, defaultBudgetCode));

        static Result<NormalizedDetails> Fail(Error error) => Result.Failure<NormalizedDetails>(error);
    }

    /// <summary>
    /// A deliberately loose shape check — one '@' with something either side and no whitespace.
    /// It catches a phone number typed into the email box; it does not pretend to validate
    /// deliverability, and nothing sends mail to this address.
    /// </summary>
    private static bool LooksLikeEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0
            && at == email.LastIndexOf('@')
            && at < email.Length - 1
            && !email.Any(char.IsWhiteSpace);
    }

    /// <summary>Blank optional text is stored as null, never as an empty string.</summary>
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
