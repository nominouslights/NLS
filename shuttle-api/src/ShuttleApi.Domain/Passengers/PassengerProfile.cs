using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Passengers;

public sealed class PassengerProfile : AggregateRoot<Guid>
{
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public DateTime LastBookedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PassengerProfile() { }

    public static PassengerProfile Create(Guid clientId, string name, string? phone, string? email)
    {
        Guard.AgainstNullOrEmpty(name, nameof(name));
        return new PassengerProfile
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            Name = name.Trim(),
            NormalizedName = name.Trim().ToLowerInvariant(),
            Phone = phone?.Trim().NullIfEmpty(),
            Email = email?.Trim().NullIfEmpty(),
            LastBookedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateLastBooked(string? phone, string? email)
    {
        LastBookedAt = DateTime.UtcNow;
        if (phone?.Trim().NullIfEmpty() is string p) Phone = p;
        if (email?.Trim().NullIfEmpty() is string e) Email = e;
    }
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
