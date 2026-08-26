namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// The latest completion of one code on one vehicle, reduced to the two facts due math
/// needs: the odometer reading and the date the work was performed.
/// </summary>
public sealed record PmLastDone(int OdometerKm, DateOnly Date);
