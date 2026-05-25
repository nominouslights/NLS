using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record UpdateContractCommand(
    Guid ContractId,
    DateTime StartDate,
    DateTime RenewalDate,
    string? Notes) : ICommand;
