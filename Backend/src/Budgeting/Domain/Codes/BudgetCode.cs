using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.Codes.Events;

namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>
/// One line of the tenant's chart of budget accounts (architecture Section 5.3): the tag every
/// dollar — cost or revenue — is attributed to. Identity (<see cref="Code"/>, <see cref="Name"/>,
/// <see cref="Description"/>), classification (<see cref="Category"/>, <see cref="ServiceLine"/>,
/// <see cref="CostCentre"/>, <see cref="ParentCodeId"/>), accounting
/// (<see cref="GlAccountCode"/>, <see cref="TaxTreatment"/>) and governance
/// (<see cref="BudgetOwnerUserId"/>, <see cref="ReviewFrequency"/>, <see cref="IsActive"/>).
/// <para>
/// <b>The code string is immutable.</b> Allocations and actual transactions reference a code by
/// that string rather than by id, so an edit that rewrote it would orphan every row already
/// tagged. Everything descriptive is editable; the code itself is decided once. Getting it wrong
/// is a deactivate-and-recreate, not a rename — there is no rename path anywhere in the stack.
/// </para>
/// <para>
/// <b>Retiring (<see cref="SetActive"/>) is the normal end of a code's life, not deletion.</b>
/// Last year's allocations and actuals must keep resolving to a code that still exists, so an
/// inactive code stays listed and simply stops being offered for new work. Hard delete exists for
/// the narrow case retirement does not cover — a code created in error that nothing has ever
/// referenced — and the application layer refuses it the moment anything points at the code.
/// </para>
/// <para>
/// <b><see cref="GlAccountCode"/> is validated for length and nothing else, by decision.</b>
/// QuickBooks work on this platform is manual: <c>Invoice.EnteredInQbo</c> is a flag a bookkeeper
/// ticks after keying an invoice in by hand, and the platform never calls the QBO API. There is
/// no OAuth-connected company, no tenant→realm mapping and no synced chart of accounts to check
/// this string against, and this story does not add one. It is a reference a person types and a
/// bookkeeper reads. Validating it later is a new slice, not a hole left open here.
/// </para>
/// Uniqueness of (tenant, code) is enforced by the create handler against the tenant's existing
/// codes, with a unique index as the double-click backstop.
/// </summary>
public sealed class BudgetCode : AggregateRoot, ITenantScoped
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 1000;
    public const int CostCentreMaxLength = 32;
    public const int GlAccountCodeMaxLength = 32;

    private BudgetCode()
    {
        // EF Core materialization only.
        Code = null!;
        Name = null!;
    }

    public Guid TenantId { get; private set; }

    /// <summary>Normalized to upper case and trimmed; set once at creation and never changed.</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    public BudgetCodeCategory Category { get; private set; }
    public BudgetServiceLine? ServiceLine { get; private set; }
    public string? CostCentre { get; private set; }

    /// <summary>
    /// Parent for one-level rollup reporting. A bare id, not a navigation property: two budget
    /// codes are two aggregates, and there is no database foreign key (the platform reserves
    /// relational links for entities inside a single aggregate boundary). The one-level
    /// guarantee is enforced in the application layer, which can see the tenant's other codes.
    /// </summary>
    public Guid? ParentCodeId { get; private set; }

    public string? GlAccountCode { get; private set; }
    public BudgetTaxTreatment? TaxTreatment { get; private set; }

    /// <summary>Accountable user, resolved through Budgeting's user_lookup replica.</summary>
    public Guid? BudgetOwnerUserId { get; private set; }

    public BudgetReviewFrequency ReviewFrequency { get; private set; }

    /// <summary>False once retired — still listed and still resolvable, just not offered for new work.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Who created and last changed this code, from the access token's <c>sub</c> claim — never
    /// from a request body. Null for anything created outside a request. Resolved to a readable
    /// email through <c>user_lookup</c> at read time rather than stored denormalized, so a future
    /// rename does not leave stale copies behind.
    /// </summary>
    public Guid? CreatedBy { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Creates an active budget code. The code string is normalized here, once.</summary>
    public static Result<BudgetCode> Create(Guid tenantId, string code, BudgetCodeDetails details, Guid? actorId)
    {
        var normalizedCode = NormalizeCode(code);

        var codeValidation = ValidateCode(normalizedCode);
        if (codeValidation.IsFailure)
        {
            return Result.Failure<BudgetCode>(codeValidation.Error);
        }

        var validation = Validate(details);
        if (validation.IsFailure)
        {
            return Result.Failure<BudgetCode>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var budgetCode = new BudgetCode
        {
            TenantId = tenantId,
            Code = normalizedCode,
            IsActive = true,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            // Apply fills the descriptive fields; the initializer keeps the compiler satisfied
            // until it runs.
            Name = string.Empty,
        };

        budgetCode.Apply(details);
        budgetCode.Raise(new BudgetCodeCreatedDomainEvent(budgetCode.Id, tenantId, budgetCode.Code, actorId));
        return Result.Success(budgetCode);
    }

    /// <summary>Rewrites the descriptive details. Never touches <see cref="Code"/> or <see cref="CreatedBy"/>.</summary>
    public Result Update(BudgetCodeDetails details, Guid? actorId)
    {
        var validation = Validate(details);
        if (validation.IsFailure)
        {
            return validation;
        }

        Apply(details);
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new BudgetCodeUpdatedDomainEvent(Id, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Retires or restores the code. A no-op when it is already in that state — and silently so,
    /// because an unchanged aggregate raises no event, which is exactly what the audit pipeline
    /// expects of a write that changed nothing.
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

        Raise(new BudgetCodeActivationChangedDomainEvent(Id, active, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Trim + upper case. Public because the create handler needs the same normalization to look
    /// for a duplicate before the aggregate exists — "zbb-crew-01" must collide with "ZBB-CREW-01".
    /// </summary>
    public static string NormalizeCode(string? code) =>
        code?.Trim().ToUpperInvariant() ?? string.Empty;

    private void Apply(BudgetCodeDetails details)
    {
        Name = details.Name.Trim();
        Description = Normalize(details.Description);
        Category = details.Category;
        ServiceLine = details.ServiceLine;
        CostCentre = Normalize(details.CostCentre);
        ParentCodeId = details.ParentCodeId;
        GlAccountCode = Normalize(details.GlAccountCode);
        TaxTreatment = details.TaxTreatment;
        BudgetOwnerUserId = details.BudgetOwnerUserId;
        ReviewFrequency = details.ReviewFrequency;
    }

    private static Result ValidateCode(string normalizedCode)
    {
        if (normalizedCode.Length == 0)
        {
            return Result.Failure(BudgetCodeErrors.CodeRequired);
        }

        if (normalizedCode.Length > CodeMaxLength)
        {
            return Result.Failure(BudgetCodeErrors.CodeTooLong);
        }

        // Letters, digits and interior hyphens only. Hand-rolled rather than a regex: the rule is
        // three conditions long, and this keeps the code readable next to the error it produces.
        if (normalizedCode[0] == '-' || normalizedCode[^1] == '-')
        {
            return Result.Failure(BudgetCodeErrors.CodeInvalidFormat);
        }

        foreach (var character in normalizedCode)
        {
            if (character != '-' && !char.IsAsciiLetterOrDigit(character))
            {
                return Result.Failure(BudgetCodeErrors.CodeInvalidFormat);
            }
        }

        return Result.Success();
    }

    private static Result Validate(BudgetCodeDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.Name))
        {
            return Result.Failure(BudgetCodeErrors.NameRequired);
        }

        if (details.Name.Trim().Length > NameMaxLength)
        {
            return Result.Failure(BudgetCodeErrors.NameTooLong);
        }

        if (details.Description?.Trim().Length > DescriptionMaxLength)
        {
            return Result.Failure(BudgetCodeErrors.DescriptionTooLong);
        }

        if (details.CostCentre?.Trim().Length > CostCentreMaxLength)
        {
            return Result.Failure(BudgetCodeErrors.CostCentreTooLong);
        }

        if (details.GlAccountCode?.Trim().Length > GlAccountCodeMaxLength)
        {
            return Result.Failure(BudgetCodeErrors.GlAccountCodeTooLong);
        }

        // Enum range checks are not redundant with model binding. JsonStringEnumConverter rejects
        // an unknown *string* with a bare 400 carrying no error code, but a numeric 99 binds
        // cleanly and would otherwise be persisted and later render as a blank label.
        if (!Enum.IsDefined(details.Category))
        {
            return Result.Failure(BudgetCodeErrors.CategoryInvalid);
        }

        if (!Enum.IsDefined(details.ReviewFrequency))
        {
            return Result.Failure(BudgetCodeErrors.ReviewFrequencyInvalid);
        }

        if (details.ServiceLine is { } serviceLine && !Enum.IsDefined(serviceLine))
        {
            return Result.Failure(BudgetCodeErrors.ServiceLineInvalid);
        }

        if (details.TaxTreatment is { } taxTreatment && !Enum.IsDefined(taxTreatment))
        {
            return Result.Failure(BudgetCodeErrors.TaxTreatmentInvalid);
        }

        return Result.Success();
    }

    /// <summary>Blank optional text is stored as null, never as an empty string.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
