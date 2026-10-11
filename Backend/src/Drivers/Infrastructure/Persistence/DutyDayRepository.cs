using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="DriversDbContext"/> (tenant-filtered).</summary>
internal sealed class DutyDayRepository(DriversDbContext context) : IDutyDayRepository
{
    public Task<DutyDay?> GetAsync(Guid driverId, DateOnly localDate, CancellationToken cancellationToken = default) =>
        context.DutyDays.FirstOrDefaultAsync(d => d.DriverId == driverId && d.LocalDate == localDate, cancellationToken);

    public async Task<IReadOnlyList<DutyDay>> LoadRangeAsync(Guid driverId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await context.DutyDays
            .AsNoTracking()
            .Where(d => d.DriverId == driverId && d.LocalDate >= from && d.LocalDate <= to)
            .OrderBy(d => d.LocalDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DutyDay>> LoadRangeForAllDriversAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        await context.DutyDays
            .AsNoTracking()
            .Where(d => d.LocalDate >= from && d.LocalDate <= to)
            .OrderBy(d => d.DriverId)
            .ThenBy(d => d.LocalDate)
            .ToListAsync(cancellationToken);

    public async Task<DateOnly?> EarliestLocalDateAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        await context.DutyDays
            .Where(d => d.DriverId == driverId)
            .OrderBy(d => d.LocalDate)
            .Select(d => (DateOnly?)d.LocalDate)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, DateOnly>> EarliestLocalDatesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await context.DutyDays
            .GroupBy(d => d.DriverId)
            .Select(g => new { DriverId = g.Key, First = g.Min(d => d.LocalDate) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.DriverId, r => r.First);
    }

    public Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default) =>
        context.Drivers.AnyAsync(d => d.Id == driverId, cancellationToken);

    public void Add(DutyDay day) => context.DutyDays.Add(day);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
