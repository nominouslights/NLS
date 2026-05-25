using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record GetContractsByClientIdQuery(Guid ClientId) : IQuery<IReadOnlyList<ContractSummaryResult>>;
