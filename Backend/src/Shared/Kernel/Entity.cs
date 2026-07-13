namespace NorthernLink.Shared.Kernel;

/// <summary>
/// Base class for all entities. Identity-based equality on <see cref="Id"/>.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected init; } = Guid.NewGuid();

    public override bool Equals(object? obj) =>
        obj is Entity other && GetType() == other.GetType() && Id == other.Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
