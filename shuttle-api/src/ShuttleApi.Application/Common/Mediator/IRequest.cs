namespace ShuttleApi.Application.Common.Mediator;

public interface IRequest<out TResponse> { }

public interface IRequest : IRequest<Unit> { }
