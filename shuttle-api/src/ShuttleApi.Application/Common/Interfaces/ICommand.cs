using ShuttleApi.Application.Common.Mediator;

namespace ShuttleApi.Application.Common.Interfaces;

public interface ICommand<TResponse> : IRequest<TResponse> { }
public interface ICommand : IRequest { }

public interface IQuery<TResponse> : IRequest<TResponse> { }
