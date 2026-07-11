using NorthernLink.SharedKernel;

namespace NorthernLink.BuildingBlocks.Application.Messaging;

/// <summary>A query that reads state and never mutates it.</summary>
public interface IQuery<TResponse>;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
