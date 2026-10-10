namespace NorthernLink.Budgeting.Application.CostCentres;

/// <summary>
/// The Budgeting module's public representation of a cost-centre register entry. The
/// <c>…Code</c>/<c>…Name</c>/<c>…Email</c> companions to the id fields are resolved by the read
/// service on every read, never stored — the <c>BudgetCodeResponse</c> convention: a user's name
/// is null until they set one, so a client renders name-or-email.
/// </summary>
public sealed record CostCentreResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    Guid? OwnerUserId,
    string? OwnerName,
    string? OwnerEmail,
    Guid? ParentId,
    string? ParentCode,
    string? ParentName,
    bool IsActive,
    Guid? CreatedBy,
    string? CreatedByName,
    string? CreatedByEmail,
    Guid? ModifiedBy,
    string? ModifiedByName,
    string? ModifiedByEmail,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
