using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Queries.Drivers;

public sealed record GetArchivedDriversQuery : IQuery<IReadOnlyList<ArchivedDriverResult>>;

public sealed record ArchivedDriverResult(
    Guid Id,
    string EmployeeId,
    string FirstName,
    string LastName,
    string FullName,
    string Phone,
    string Email,
    string Status,
    bool IsActive,
    DateTime? DeletedAt);
