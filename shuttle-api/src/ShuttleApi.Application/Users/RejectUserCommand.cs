using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Users;

public sealed record RejectUserCommand(Guid UserId) : ICommand;
