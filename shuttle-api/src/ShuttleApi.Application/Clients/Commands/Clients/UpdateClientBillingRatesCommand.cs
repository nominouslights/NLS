using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;

namespace ShuttleApi.Application.Clients;

public sealed record UpdateClientBillingRatesCommand(
    Guid ClientId,
    decimal? RunRateOneWay,
    decimal? RunRateRoundTrip,
    decimal? CargoRatePerKg) : ICommand<Unit>;
