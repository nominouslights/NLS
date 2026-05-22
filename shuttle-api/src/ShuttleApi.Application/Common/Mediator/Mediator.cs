using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace ShuttleApi.Application.Common.Mediator;

internal sealed class Mediator(IServiceProvider serviceProvider) : ISender
{
    private static readonly ConcurrentDictionary<Type, object> _wrapperCache = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (HandlerWrapperBase<TResponse>)_wrapperCache.GetOrAdd(
            request.GetType(),
            static (requestType, responseType) =>
                Activator.CreateInstance(typeof(HandlerWrapper<,>).MakeGenericType(requestType, responseType))!,
            typeof(TResponse));

        return wrapper.Handle(request, serviceProvider, cancellationToken);
    }

    public async Task Send(IRequest request, CancellationToken cancellationToken = default)
        => await Send<Unit>((IRequest<Unit>)request, cancellationToken);

    private abstract class HandlerWrapperBase<TResponse>
    {
        public abstract Task<TResponse> Handle(object request, IServiceProvider sp, CancellationToken ct);
    }

    private sealed class HandlerWrapper<TRequest, TResponse> : HandlerWrapperBase<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Handle(object request, IServiceProvider sp, CancellationToken ct)
        {
            var typedRequest = (TRequest)request;
            var handler = sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
            var behaviors = sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

            RequestHandlerDelegate<TResponse> pipeline = c => handler.Handle(typedRequest, c);

            for (int i = behaviors.Length - 1; i >= 0; i--)
            {
                var next = pipeline;
                var b = behaviors[i];
                pipeline = c => b.Handle(typedRequest, next, c);
            }

            return pipeline(ct);
        }
    }
}
