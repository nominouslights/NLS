using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record GetRateLinesByClientQuery(Guid ClientId) : IQuery<IReadOnlyList<RateLineResult>>;
