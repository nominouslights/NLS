using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record UpdateContractCommand(
    Guid ContractId,
    DateTime StartDate,
    DateTime EndDate,
    string? Notes) : ICommand;
