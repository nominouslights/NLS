using NorthernLink.Shared.Kernel;

namespace NorthernLink.Shared.Messaging;

/// <summary>
/// In-process command/query dispatcher. The platform's replacement for MediatR
/// (which moved to a commercial license) — resolves the matching handler from DI.
/// </summary>
public interface ISender
{
    Task<Result> Send(ICommand command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    Task<Result<TResponse>> Query<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
