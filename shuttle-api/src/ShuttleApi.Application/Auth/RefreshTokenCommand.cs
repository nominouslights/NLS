using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Auth;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<LoginResult>;
