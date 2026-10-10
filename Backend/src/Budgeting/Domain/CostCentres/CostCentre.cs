using NorthernLink.Shared.Kernel;
using NorthernLink.Budgeting.Domain.CostCentres.Events;

namespace NorthernLink.Budgeting.Domain.CostCentres;

/// <summary>
/// One entry of the tenant's cost-centre register: an organisational unit or base that cost is
/// attributed to (owner decision — a cost centre is a unit or base, never a vehicle).
/// <para>
/// <b>Tenant-wide, not per period.</b> Budget codes belong to a period and are re-justified each
/// one; the organisation they are attributed to is not. A budget code still carries its cost
/// centre as the <em>code string</em> (<c>BudgetCode.CostCentre</c>) — strings are the platform's
/// cross-period join key — and the register is what that string is validated against.
/// </para>
/// <para>
/// <b>The code is immutable, and normalized exactly as <c>BudgetCode.CostCentre</c> always has
/// been: trimmed, case preserved.</b> Upper-casing here would make every existing mixed-case
/// cost centre on a budget code stop matching its own register entry. Uniqueness is ordinal on
/// the trimmed string, per tenant (unique index (tenant_id, code) as the race backstop).
/// </para>
/// <para>
/// <b>Retiring (<see cref="SetActive"/>) is the normal end of life.</b> A retired entry stops
/// being offered for new budget codes, but codes that already carry it keep it — an edit that
/// leaves the value unchanged is still accepted. Hard delete is only for an entry nothing has
/// ever referenced.
/// </para>
/// </summary>
public sealed class CostCentre : AggregateRoot, ITenantScoped
{
    /// <summary>Matches <c>BudgetCode.CostCentreMaxLength</c> — the column the code is written into.</summary>
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 1000;

    private CostCentre()
    {
        // EF Core materialization only.
        Code = null!;
        Name = null!;
    }

    public Guid TenantId { get; private set; }

    /// <summary>Trimmed, case preserved; set once at creation and never changed.</summary>
    public string Code { get; private set; }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    /// <summary>Accountable user, resolved through Budgeting's user_lookup replica.</summary>
    public Guid? OwnerUserId { get; private set; }

    /// <summary>
    /// Parent for a one-level rollup. A bare id: two cost centres are two aggregates, and there
    /// is no database foreign key. The one-level rule is enforced in the application layer,
    /// which can see the rest of the register.
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>False once retired — still listed and still resolvable, just not offered for new codes.</summary>
    public bool IsActive { get; private set; }

    /// <summary>From the access token's <c>sub</c> claim, never a request body.</summary>
    public Guid? CreatedBy { get; private set; }

    public Guid? ModifiedBy { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Creates an active cost centre. The code is normalized here, once.</summary>
    public static Result<CostCentre> Create(
        Guid tenantId, string? code, CostCentreDetails details, Guid? actorId)
    {
        var normalizedCode = NormalizeCode(code);

        if (normalizedCode.Length == 0)
        {
            return Result.Failure<CostCentre>(CostCentreErrors.CodeRequired);
        }

        if (normalizedCode.Length > CodeMaxLength)
        {
            return Result.Failure<CostCentre>(CostCentreErrors.CodeTooLong);
        }

        var validation = Validate(details);
        if (validation.IsFailure)
        {
            return Result.Failure<CostCentre>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var costCentre = new CostCentre
        {
            TenantId = tenantId,
            Code = normalizedCode,
            IsActive = true,
            CreatedBy = actorId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            // Apply fills the descriptive fields.
            Name = string.Empty,
        };

        costCentre.Apply(details);
        costCentre.Raise(new CostCentreCreatedDomainEvent(costCentre.Id, tenantId, costCentre.Code, actorId));
        return Result.Success(costCentre);
    }

    /// <summary>
    /// Rewrites the descriptive details. Never touches <see cref="Code"/> or
    /// <see cref="CreatedBy"/>. An edit that changes nothing (after normalization) is a silent
    /// success: no event and no modified stamp, so a repeated save writes nothing.
    /// </summary>
    public Result Update(CostCentreDetails details, Guid? actorId)
    {
        var validation = Validate(details);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (details.ParentId is { } parentId && parentId == Id)
        {
            return Result.Failure(CostCentreErrors.ParentIsSelf);
        }

        if (Name == details.Name.Trim()
            && Description == Normalize(details.Description)
            && OwnerUserId == details.OwnerUserId
            && ParentId == details.ParentId)
        {
            return Result.Success();
        }

        Apply(details);
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new CostCentreUpdatedDomainEvent(Id, actorId));
        return Result.Success();
    }

    /// <summary>Retires or restores the entry. A silent no-op (no event) when already in that state.</summary>
    public Result SetActive(bool active, Guid? actorId)
    {
        if (IsActive == active)
        {
            return Result.Success();
        }

        IsActive = active;
        ModifiedBy = actorId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new CostCentreActivationChangedDomainEvent(Id, active, actorId));
        return Result.Success();
    }

    /// <summary>
    /// Trim only — exactly how <c>BudgetCode</c> has always normalized its cost-centre string, so
    /// a register entry and the value already on a budget code compare equal ordinally. Public
    /// because handlers normalize before a lookup (duplicate check, budget-code validation).
    /// Null and whitespace normalize to the empty string.
    /// </summary>
    public static string NormalizeCode(string? code) => code?.Trim() ?? string.Empty;

    private void Apply(CostCentreDetails details)
    {
        Name = details.Name.Trim();
        Description = Normalize(details.Description);
        OwnerUserId = details.OwnerUserId;
        ParentId = details.ParentId;
    }

    private static Result Validate(CostCentreDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.Name))
        {
            return Result.Failure(CostCentreErrors.NameRequired);
        }

        if (details.Name.Trim().Length > NameMaxLength)
        {
            return Result.Failure(CostCentreErrors.NameTooLong);
        }

        if (details.Description?.Trim().Length > DescriptionMaxLength)
        {
            return Result.Failure(CostCentreErrors.DescriptionTooLong);
        }

        return Result.Success();
    }

    /// <summary>Blank optional text is stored as null, never as an empty string.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
