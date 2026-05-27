using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Queries.Drivers;

public sealed record GetDriversQuery : IQuery<IReadOnlyList<DriverListItemResult>>;

public sealed record DriverListItemResult(
    Guid Id,
    string EmployeeId,
    string FirstName,
    string LastName,
    string FullName,
    string Phone,
    string Email,
    string Status,
    bool IsActive,
    bool HasExpiringDocuments);
