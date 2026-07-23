using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.Contracts.Terminate;

/// <summary>Cuts an active contract short (Status → Terminated).</summary>
public sealed record TerminateContractCommand(Guid TenantId, Guid ContractId) : ICommand;
