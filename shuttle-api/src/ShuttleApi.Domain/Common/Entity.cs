namespace ShuttleApi.Domain.Common;

public abstract class Entity<TId>
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id) => Id = id;

    protected Entity() { }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other &&
        GetType() == other.GetType() &&
        EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? a, Entity<TId>? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(Entity<TId>? a, Entity<TId>? b) => !(a == b);
}
