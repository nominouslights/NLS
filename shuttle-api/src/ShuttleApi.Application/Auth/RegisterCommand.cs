using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Users;

namespace ShuttleApi.Application.Auth;

public sealed record RegisterCommand(string Email, string Password, UserRole Role) : ICommand<RegisterResult>;
