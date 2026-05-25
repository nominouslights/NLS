using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Clients;

public sealed record DeleteClientCommand(Guid Id) : ICommand;
