using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Clients;

namespace ShuttleApi.Application.Clients;

public sealed record GetClientsQuery : IQuery<IReadOnlyList<ClientListItemResult>>;

public sealed record ClientListItemResult(
    Guid Id,
    string BusinessName,
    string ServiceType,
    string PrimaryContactName,
    string Phone,
    string Email,
    bool IsActive,
    DateTime? ActiveContractEndDate,
    bool IsExpiringSoon);
