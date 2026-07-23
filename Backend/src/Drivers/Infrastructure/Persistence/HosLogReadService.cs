using Microsoft.EntityFrameworkCore;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Drivers.Infrastructure.Persistence;

/// <summary>Read side — queries drivers.rm_hos_log_entries and maps to the public contract.</summary>
internal sealed class HosLogReadService(DriversDbContext context) : IHosLogReadService
{
    public async Task<IReadOnlyList<HosEntryResponse>> GetForDriverAsync(
        Guid driverId, CancellationToken cancellationToken = default)
    {
        var entries = await context.HosLogEntryReadModels
            .AsNoTracking()
            .Where(e => e.DriverId == driverId)
            .OrderByDescending(e => e.Date)
            .ThenByDescending(e => e.RecordedAtUtc)
            .ToListAsync(cancellationToken);

        return entries.Select(ToResponse).ToList();
    }

    private static HosEntryResponse ToResponse(HosLogEntryReadModel e) => new(
        e.Id,
        e.DriverId,
        e.Date,
        HosDisplay.DutyToWire(e.Duty),
        e.OnDutyHours,
        e.DrivingHours,
        e.OffDutyHours,
        HosDisplay.SourceToWire(e.Source),
        e.EnteredBy,
        e.Note,
        e.RecordedAtUtc);
}
