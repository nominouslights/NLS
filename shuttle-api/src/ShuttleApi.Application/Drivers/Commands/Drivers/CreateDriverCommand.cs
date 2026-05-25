using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

public sealed record CreateDriverCommand(
    string EmployeeId,
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    DateTime HireDate) : ICommand<CreateDriverResult>;

public sealed record CreateDriverResult(Guid Id);
