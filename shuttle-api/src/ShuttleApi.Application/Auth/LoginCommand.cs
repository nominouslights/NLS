using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Auth;

public sealed record LoginCommand(string Email, string Password) : ICommand<LoginResult>;
