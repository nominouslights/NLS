using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Setup;

public sealed record InitializeSystemCommand(string Email, string Password) : ICommand;
