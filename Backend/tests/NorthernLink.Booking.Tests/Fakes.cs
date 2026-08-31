using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Domain.Settings;

namespace NorthernLink.Booking.Tests;

/// <summary>In-memory fake of the policy repository for handler tests.</summary>
internal sealed class InMemoryBookingPolicyRepository : IBookingPolicyRepository
{
    public BookingPolicy? Policy { get; set; }

    public int SaveChangesCallCount { get; private set; }

    public Task<BookingPolicy?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Policy);

    public void Add(BookingPolicy policy) => Policy = policy;

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory fake of the corridor settings repository.</summary>
internal sealed class InMemoryCorridorSettingsRepository : ICorridorSettingsRepository
{
    public List<CorridorBookingSettings> Settings { get; } = [];

    public Task<CorridorBookingSettings?> GetByCorridorAsync(
        Guid corridorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Settings.FirstOrDefault(s => s.CorridorId == corridorId));

    public Task<IReadOnlyList<CorridorBookingSettings>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CorridorBookingSettings>>(Settings);

    public void Add(CorridorBookingSettings settings) => Settings.Add(settings);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>In-memory fake of the corridor replica.</summary>
internal sealed class InMemoryCorridorLookupRepository : ICorridorLookupRepository
{
    public List<CorridorLookup> Corridors { get; } = [];

    public Task<CorridorLookup?> GetAsync(Guid corridorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Corridors.FirstOrDefault(c => c.CorridorId == corridorId));

    public Task<IReadOnlyList<CorridorLookup>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CorridorLookup>>(Corridors);

    public Task UpsertAsync(CorridorLookup corridor, CancellationToken cancellationToken = default)
    {
        Corridors.RemoveAll(c => c.CorridorId == corridor.CorridorId && c.TenantId == corridor.TenantId);
        Corridors.Add(corridor);
        return Task.CompletedTask;
    }
}
