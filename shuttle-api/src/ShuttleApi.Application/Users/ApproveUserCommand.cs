using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Users;

public sealed record ApproveUserCommand(Guid UserId) : ICommand;
