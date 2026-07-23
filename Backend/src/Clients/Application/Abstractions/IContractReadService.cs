using NorthernLink.Clients.Application.Contracts;

namespace NorthernLink.Clients.Application.Abstractions;

/// <summary>Read side for contract queries — returns response DTOs directly (tenant-scoped).</summary>
public interface IContractReadService
{
    Task<IReadOnlyList<ContractResponse>> GetForClientAsync(Guid clientId, CancellationToken cancellationToken = default);
}
