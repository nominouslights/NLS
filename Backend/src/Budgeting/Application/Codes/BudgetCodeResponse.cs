namespace NorthernLink.Budgeting.Application.Codes;

/// <summary>
/// The Budgeting module's public representation of a budget code — the shape the Budgeting
/// console's Budget Codes screen consumes. Enums travel as their PascalCase names
/// (<paramref name="Category"/>, <paramref name="ServiceLine"/>, <paramref name="TaxTreatment"/>,
/// <paramref name="ReviewFrequency"/>).
/// <para>
/// The <c>…Code</c>/<c>…Name</c>/<c>…Email</c> companions to the three id fields are resolved by
/// the read service, not stored: they exist so a screen can render a parent or an owner without a
/// second round trip, and they are recomputed on every read so they cannot go stale.
/// </para>
/// <para>
/// Each of the three user ids gets both a <c>…Name</c> and a <c>…Email</c>. The name is null for
/// anyone who has not set a profile, so a client renders name-or-email; sending one merged
/// display string instead would take away its ability to show the name with the email beneath it,
/// and resolving only the owner would leave "created by" and "last modified by" reading as raw
/// addresses directly below it.
/// </para>
/// </summary>
public sealed record BudgetCodeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Category,
    string? ServiceLine,
    string? CostCentre,
    Guid? ParentCodeId,
    string? ParentCode,
    string? ParentName,
    string? GlAccountCode,
    string? TaxTreatment,
    Guid? BudgetOwnerUserId,
    string? BudgetOwnerName,
    string? BudgetOwnerEmail,
    string ReviewFrequency,
    bool IsActive,
    Guid? CreatedBy,
    string? CreatedByName,
    string? CreatedByEmail,
    Guid? ModifiedBy,
    string? ModifiedByName,
    string? ModifiedByEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
