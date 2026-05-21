using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Users;

public sealed record GetPendingUsersQuery : IQuery<IReadOnlyList<PendingUserResult>>;

public sealed record PendingUserResult(Guid Id, string Email, string Role, DateTime CreatedAt);
