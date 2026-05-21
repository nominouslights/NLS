namespace ShuttleApi.Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is ValueObject other &&
        GetType() == other.GetType() &&
        GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(0, (hash, component) =>
                HashCode.Combine(hash, component?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? a, ValueObject? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(ValueObject? a, ValueObject? b) => !(a == b);
}
