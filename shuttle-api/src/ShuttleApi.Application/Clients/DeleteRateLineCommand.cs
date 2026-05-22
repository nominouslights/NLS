using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record DeleteRateLineCommand(Guid RateLineId) : ICommand;
