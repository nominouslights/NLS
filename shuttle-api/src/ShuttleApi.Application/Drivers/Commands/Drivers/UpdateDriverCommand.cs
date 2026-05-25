using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Commands.Drivers;

public sealed record UpdateDriverCommand(
    Guid Id,
    string EmployeeId,
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    DateTime HireDate,
    bool IsActive) : ICommand;
