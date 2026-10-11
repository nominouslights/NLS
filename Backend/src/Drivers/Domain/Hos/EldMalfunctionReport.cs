using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// An ELD malfunction report (s.78.2): the bus's ELD stopped recording, so from
/// <see cref="NoticedAtUtc"/> the driver keeps paper logs and dispatch transcribes them into the
/// ledger. Reported by the driver (own record) or dispatch; a compliance officer resolves it once
/// the device is repaired. While one is <see cref="EldMalfunctionStatus.Open"/> the status
/// response says <c>eldMalfunctionOpen</c> and dispatch warns — it never blocks, paper logs being
/// the sanctioned fallback. The rule engine receives it as information only.
/// </summary>
public sealed class EldMalfunctionReport : AggregateRoot, ITenantScoped
{
    public const int CodeMaxLength = 32;
    public const int DescriptionMaxLength = 1000;

    private EldMalfunctionReport()
    {
        // EF Core materialization only.
        Code = null!;
        Description = null!;
        DriverName = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }

    /// <summary>
    /// The driver's name as it was when reported — a snapshot, so the integration event (and
    /// the compliance alert email built from it) can name the driver without a lookup.
    /// </summary>
    public string DriverName { get; private set; }

    /// <summary>The bus the ELD is on, when known. Opaque — no reference into Fleet.</summary>
    public Guid? VehicleId { get; private set; }

    /// <summary>The code the device displayed (e.g. "P", "E", "T", or the vendor's own).</summary>
    public string Code { get; private set; }

    public DateTimeOffset NoticedAtUtc { get; private set; }
    public DateTimeOffset ReportedAtUtc { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public string Description { get; private set; }
    public EldMalfunctionStatus Status { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public string? ResolutionNote { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsOpen => Status == EldMalfunctionStatus.Open;

    /// <summary>True when the malfunction was unresolved somewhere inside [<paramref name="fromUnix"/>, <paramref name="toUnix"/>).</summary>
    public bool Overlaps(long fromUnix, long toUnix)
    {
        var from = NoticedAtUtc.ToUnixTimeSeconds();
        var to = ResolvedAtUtc?.ToUnixTimeSeconds() ?? long.MaxValue;
        return from < toUnix && to > fromUnix;
    }

    public static Result<EldMalfunctionReport> Report(
        Guid tenantId,
        Guid driverId,
        string driverName,
        Guid? vehicleId,
        string? code,
        DateTimeOffset noticedAtUtc,
        Guid reportedByUserId,
        DateTimeOffset now,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<EldMalfunctionReport>(ComplianceErrors.EldCodeRequired);
        }

        if (code.Trim().Length > CodeMaxLength)
        {
            return Result.Failure<EldMalfunctionReport>(ComplianceErrors.EldCodeTooLong);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<EldMalfunctionReport>(ComplianceErrors.EldDescriptionRequired);
        }

        if (noticedAtUtc > now.AddSeconds(HosLimits.FutureSkewSeconds))
        {
            return Result.Failure<EldMalfunctionReport>(ComplianceErrors.EldNoticedInFuture);
        }

        var trimmed = description.Trim();
        var report = new EldMalfunctionReport
        {
            TenantId = tenantId,
            DriverId = driverId,
            DriverName = string.IsNullOrWhiteSpace(driverName) ? string.Empty : driverName.Trim(),
            VehicleId = vehicleId,
            Code = code.Trim(),
            NoticedAtUtc = noticedAtUtc,
            ReportedAtUtc = now,
            ReportedByUserId = reportedByUserId,
            Description = trimmed.Length > DescriptionMaxLength ? trimmed[..DescriptionMaxLength] : trimmed,
            Status = EldMalfunctionStatus.Open,
            UpdatedAtUtc = now,
        };

        report.Raise(new EldMalfunctionReportedDomainEvent(report.Id, driverId, tenantId, report.Code));
        return Result.Success(report);
    }

    /// <summary>A compliance officer confirms the repair. Resolving twice is a no-op.</summary>
    public Result Resolve(Guid byUserId, DateTimeOffset now, string? note)
    {
        if (Status == EldMalfunctionStatus.Resolved)
        {
            return Result.Success();
        }

        Status = EldMalfunctionStatus.Resolved;
        ResolvedAtUtc = now;
        ResolvedByUserId = byUserId;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAtUtc = now;

        Raise(new EldMalfunctionResolvedDomainEvent(Id, DriverId, TenantId));
        return Result.Success();
    }
}
