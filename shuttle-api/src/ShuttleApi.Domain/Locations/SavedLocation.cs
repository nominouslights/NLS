using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Locations;

public sealed class SavedLocation : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private SavedLocation() { }

    public static SavedLocation Create(
        string name,
        string? address,
        double? latitude,
        double? longitude)
    {
        return new SavedLocation
        {
            Id = Guid.NewGuid(),
            Name = name,
            Address = address,
            Latitude = latitude,
            Longitude = longitude,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string? address,
        double? latitude,
        double? longitude)
    {
        Name = name;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        Guard.Against(!IsDeleted, "Location is not archived.");
        IsDeleted = false;
        DeletedAt = null;
    }
}
