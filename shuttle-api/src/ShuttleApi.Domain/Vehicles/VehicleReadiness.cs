namespace ShuttleApi.Domain.Vehicles;

/// <summary>
/// Computes an operational readiness score (0–100) and produces human-readable
/// alerts for a vehicle. Logic lives in the domain so it can be called from any
/// query handler without duplication.
/// </summary>
public static class VehicleReadiness
{
    /// <summary>
    /// Returns a 0–100 readiness score.
    /// Deductions:
    ///   -25  vehicle is not Active
    ///   -15  registration expired or expiring within 30 days
    ///   -15  insurance expired or expiring within 30 days
    ///   up to -30  for open Urgent/Critical unplanned repairs (-10 per item, max 3)
    ///   up to -20  for overdue scheduled maintenance items (-5 per item, max 4)
    /// </summary>
    public static int ComputeScore(Vehicle vehicle)
    {
        var score = 100;

        if (vehicle.Status != VehicleStatus.Active)
            score -= 25;

        if (vehicle.RegistrationExpiry.HasValue &&
            vehicle.RegistrationExpiry.Value <= DateTime.UtcNow.AddDays(30))
            score -= 15;

        if (vehicle.InsuranceExpiry.HasValue &&
            vehicle.InsuranceExpiry.Value <= DateTime.UtcNow.AddDays(30))
            score -= 15;

        var now = DateTime.UtcNow;

        // Open urgent/critical unplanned repairs
        var criticalRepairs = vehicle.ServiceRecords
            .Count(r => !r.IsPlanned &&
                        r.Priority is ServicePriority.Urgent or ServicePriority.Critical &&
                        r.ServiceStatus is ServiceStatus.Scheduled or ServiceStatus.InProgress);

        score -= Math.Min(criticalRepairs * 10, 30);

        // Overdue scheduled maintenance (past scheduled date, not completed/cancelled)
        var overdueItems = vehicle.ServiceRecords
            .Count(r => r.IsPlanned &&
                        r.ScheduledDate.HasValue &&
                        r.ScheduledDate.Value < now &&
                        r.ServiceStatus is ServiceStatus.Scheduled or ServiceStatus.Deferred);

        score -= Math.Min(overdueItems * 5, 20);

        return Math.Max(score, 0);
    }

    /// <summary>
    /// Returns a list of human-readable warning strings for display in the UI.
    /// </summary>
    public static IReadOnlyList<string> GetAlerts(Vehicle vehicle)
    {
        var alerts = new List<string>();
        var now = DateTime.UtcNow;

        if (vehicle.Status == VehicleStatus.OutOfService)
            alerts.Add($"Out of service: {vehicle.StatusNote}");
        else if (vehicle.Status == VehicleStatus.InMaintenance)
            alerts.Add("Unit is currently in maintenance");
        else if (vehicle.Status == VehicleStatus.Retired)
            alerts.Add("Unit has been retired from service");

        if (vehicle.RegistrationExpiry.HasValue)
        {
            var regDays = (int)(vehicle.RegistrationExpiry.Value - now).TotalDays;
            if (regDays < 0)
                alerts.Add("Registration has expired");
            else if (regDays <= 30)
                alerts.Add($"Registration expires in {regDays} day{(regDays == 1 ? "" : "s")}");
        }

        if (vehicle.InsuranceExpiry.HasValue)
        {
            var insDays = (int)(vehicle.InsuranceExpiry.Value - now).TotalDays;
            if (insDays < 0)
                alerts.Add("Insurance has expired");
            else if (insDays <= 30)
                alerts.Add($"Insurance expires in {insDays} day{(insDays == 1 ? "" : "s")}");
        }

        var criticalRepairs = vehicle.ServiceRecords
            .Where(r => !r.IsPlanned &&
                        r.Priority is ServicePriority.Urgent or ServicePriority.Critical &&
                        r.ServiceStatus is ServiceStatus.Scheduled or ServiceStatus.InProgress)
            .ToList();

        if (criticalRepairs.Count == 1)
            alerts.Add($"1 open {criticalRepairs[0].Priority.ToString().ToLowerInvariant()} repair: {criticalRepairs[0].Title}");
        else if (criticalRepairs.Count > 1)
            alerts.Add($"{criticalRepairs.Count} open urgent/critical repairs");

        var overdueCount = vehicle.ServiceRecords
            .Count(r => r.IsPlanned &&
                        r.ScheduledDate.HasValue &&
                        r.ScheduledDate.Value < now &&
                        r.ServiceStatus is ServiceStatus.Scheduled or ServiceStatus.Deferred);

        if (overdueCount == 1)
            alerts.Add("1 overdue maintenance item");
        else if (overdueCount > 1)
            alerts.Add($"{overdueCount} overdue maintenance items");

        var expiringInspections = vehicle.InspectionRecords
            .Where(i => i.ExpiresAt.HasValue && i.ExpiresAt.Value <= now.AddDays(60))
            .OrderBy(i => i.ExpiresAt)
            .ToList();

        foreach (var insp in expiringInspections)
        {
            var days = (int)(insp.ExpiresAt!.Value - now).TotalDays;
            if (days < 0)
                alerts.Add($"{insp.InspectionType} inspection has expired");
            else
                alerts.Add($"{insp.InspectionType} inspection expires in {days} day{(days == 1 ? "" : "s")}");
        }

        return alerts.AsReadOnly();
    }
}
