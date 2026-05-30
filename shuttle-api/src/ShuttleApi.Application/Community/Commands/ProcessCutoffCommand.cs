using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Community.Commands;

public sealed record ProcessCutoffCommand : ICommand<ProcessCutoffResult>;

public sealed record ProcessCutoffResult(
    int OpenedForPayment,
    int Released,
    int TripsCancelled);
