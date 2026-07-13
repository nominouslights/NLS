using NorthernLink.Shared.Kernel;

namespace NorthernLink.Shared.Messaging;

/// <summary>A command that changes state and returns no value.</summary>
public interface ICommand;

/// <summary>A command that changes state and returns a value.</summary>
public interface ICommand<TResponse>;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
