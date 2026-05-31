using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Auth;

public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand;
