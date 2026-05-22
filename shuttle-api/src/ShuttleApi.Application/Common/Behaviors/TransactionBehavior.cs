using Microsoft.Extensions.Logging;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Common.Mediator;

namespace ShuttleApi.Application.Common.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogDebug("Beginning transaction for {RequestName}", typeof(TRequest).Name);
        var response = await next(cancellationToken);
        logger.LogDebug("Transaction complete for {RequestName}", typeof(TRequest).Name);
        return response;
    }
}
