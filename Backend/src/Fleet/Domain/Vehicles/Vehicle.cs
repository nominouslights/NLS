using NorthernLink.Fleet.Domain.Vehicles.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Vehicles;

/// <summary>
/// A fleet vehicle — the single source of truth trips and DVIRs reference (US-1.1.1).
/// Owns its lifecycle (status transition matrix), monotonic odometer with auto-retire at
/// end of service life, and km-based straight-line depreciation.
/// </summary>
public sealed class Vehicle : AggregateRoot, ITenantScoped
{
    public const string EndOfLifeRetirementReason = "End of service life reached";

    private Vehicle()
    {
        // EF Core materialization only.
        UnitNumber = null!;
        Vin = null!;
        Make = null!;
        Model = null!;
        LicencePlate = null!;
        RequiredLicenceClass = null!;
    }

    public Guid TenantId { get; private set; }
    public string UnitNumber { get; private set; }
    public Vin Vin { get; private set; }
    public string Make { get; private set; }
    public string Model { get; private set; }
    public int Year { get; private set; }
    public int SeatingCapacity { get; private set; }
    public string LicencePlate { get; private set; }
    public string RequiredLicenceClass { get; private set; }
    public VehicleStatus Status { get; private set; }
    public string? StatusReason { get; private set; }
    public int OdometerKm { get; private set; }
    public decimal AcquisitionCostCad { get; private set; }
    public int EndOfLifeKm { get; private set; }
    public decimal? SalePriceCad { get; private set; }
    public DateTimeOffset? DisposedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>NSC-11: vehicles seating 11 or more require periodic inspections.</summary>
    public bool RequiresPeriodicInspection => SeatingCapacity >= 11;

    /// <summary>Straight-line km depreciation: cost × max(0, 1 − odometer/endOfLife).</summary>
    public decimal CurrentValueCad =>
        Math.Round(AcquisitionCostCad * Math.Max(0m, 1m - (decimal)OdometerKm / EndOfLifeKm), 2);

    public int RemainingKm => Math.Max(0, EndOfLifeKm - OdometerKm);

    /// <summary>Percentage of service life used, capped at 100.</summary>
    public decimal LifeUsedPct =>
        Math.Min(100m, Math.Round((decimal)OdometerKm / EndOfLifeKm * 100m, 1));

    public bool IsDisposed => Status is VehicleStatus.Sold or VehicleStatus.Recycled;

    /// <summary>The lifecycle transition matrix. Diagonal (same status) is not a transition.</summary>
    public static bool CanTransition(VehicleStatus from, VehicleStatus to) =>
        (from, to) switch
        {
            (VehicleStatus.Active, VehicleStatus.InMaintenance or VehicleStatus.OutOfService or VehicleStatus.Retired) => true,
            (VehicleStatus.InMaintenance, VehicleStatus.Active or VehicleStatus.OutOfService or VehicleStatus.Retired) => true,
            (VehicleStatus.OutOfService, VehicleStatus.Active or VehicleStatus.InMaintenance or VehicleStatus.Retired) => true,
            (VehicleStatus.Retired, VehicleStatus.Sold or VehicleStatus.Recycled) => true,
            _ => false,
        };

    public static Result<Vehicle> Register(
        Guid tenantId,
        string unitNumber,
        Vin vin,
        string make,
        string model,
        int year,
        int seatingCapacity,
        string licencePlate,
        string requiredLicenceClass,
        int odometerKm,
        decimal acquisitionCostCad,
        int endOfLifeKm)
    {
        var validation = ValidateDetails(unitNumber, year, seatingCapacity, odometerKm, acquisitionCostCad, endOfLifeKm);
        if (validation.IsFailure)
        {
            return Result.Failure<Vehicle>(validation.Error);
        }

        var now = DateTimeOffset.UtcNow;
        var vehicle = new Vehicle
        {
            TenantId = tenantId,
            UnitNumber = unitNumber.Trim(),
            Vin = vin,
            Make = make.Trim(),
            Model = model.Trim(),
            Year = year,
            SeatingCapacity = seatingCapacity,
            LicencePlate = licencePlate.Trim(),
            RequiredLicenceClass = requiredLicenceClass.Trim(),
            Status = VehicleStatus.Active,
            OdometerKm = odometerKm,
            AcquisitionCostCad = acquisitionCostCad,
            EndOfLifeKm = endOfLifeKm,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        vehicle.Raise(new VehicleRegisteredDomainEvent(vehicle.Id, tenantId));
        return Result.Success(vehicle);
    }

    /// <summary>Updates registration details. Status and odometer change through their own methods.</summary>
    public Result UpdateDetails(
        string unitNumber,
        Vin vin,
        string make,
        string model,
        int year,
        int seatingCapacity,
        string licencePlate,
        string requiredLicenceClass,
        decimal acquisitionCostCad,
        int endOfLifeKm)
    {
        if (IsDisposed)
        {
            return Result.Failure(VehicleErrors.Disposed);
        }

        var validation = ValidateDetails(unitNumber, year, seatingCapacity, OdometerKm, acquisitionCostCad, endOfLifeKm);
        if (validation.IsFailure)
        {
            return validation;
        }

        UnitNumber = unitNumber.Trim();
        Vin = vin;
        Make = make.Trim();
        Model = model.Trim();
        Year = year;
        SeatingCapacity = seatingCapacity;
        LicencePlate = licencePlate.Trim();
        RequiredLicenceClass = requiredLicenceClass.Trim();
        AcquisitionCostCad = acquisitionCostCad;
        EndOfLifeKm = endOfLifeKm;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        return Result.Success();
    }

    /// <summary>
    /// Moves the vehicle through the transition matrix. Sold/Recycled are rejected here —
    /// disposal captures extra data and must go through <see cref="Dispose"/>.
    /// </summary>
    public Result ChangeStatus(VehicleStatus newStatus, string? reason = null)
    {
        if (IsDisposed)
        {
            return Result.Failure(VehicleErrors.Disposed);
        }

        if (newStatus is VehicleStatus.Sold or VehicleStatus.Recycled)
        {
            return Result.Failure(VehicleErrors.InvalidStatusTransition(Status, newStatus));
        }

        if (!CanTransition(Status, newStatus))
        {
            return Result.Failure(VehicleErrors.InvalidStatusTransition(Status, newStatus));
        }

        if (newStatus == VehicleStatus.OutOfService && string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(VehicleErrors.StatusReasonRequired);
        }

        var previous = Status;
        Status = newStatus;
        StatusReason = newStatus switch
        {
            VehicleStatus.Retired => string.IsNullOrWhiteSpace(reason) ? EndOfLifeRetirementReason : reason!.Trim(),
            _ => string.IsNullOrWhiteSpace(reason) ? null : reason!.Trim(),
        };
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        Raise(new VehicleStatusChangedDomainEvent(Id, previous, newStatus, StatusReason));
        return Result.Success();
    }

    /// <summary>
    /// Records a new odometer reading (monotonic). Crossing <see cref="EndOfLifeKm"/>
    /// auto-retires the vehicle unless it is already Retired, Sold, or Recycled.
    /// </summary>
    public Result RecordOdometer(int odometerKm)
    {
        if (IsDisposed)
        {
            return Result.Failure(VehicleErrors.Disposed);
        }

        if (odometerKm < OdometerKm)
        {
            return Result.Failure(VehicleErrors.OdometerRollback);
        }

        OdometerKm = odometerKm;
        UpdatedAtUtc = DateTimeOffset.UtcNow;

        if (odometerKm >= EndOfLifeKm && Status != VehicleStatus.Retired)
        {
            var previous = Status;
            Status = VehicleStatus.Retired;
            StatusReason = EndOfLifeRetirementReason;

            Raise(new VehicleStatusChangedDomainEvent(Id, previous, VehicleStatus.Retired, StatusReason));
            Raise(new VehicleReachedEndOfLifeDomainEvent(Id, odometerKm, EndOfLifeKm));
        }

        return Result.Success();
    }

    /// <summary>
    /// Terminal disposal of a retired vehicle. Sold captures an optional sale price;
    /// recording the proceeds against a Budget Code is a Billing-module follow-up.
    /// </summary>
    public Result Dispose(DisposalMethod method, decimal? salePriceCad = null, string? note = null)
    {
        if (IsDisposed)
        {
            return Result.Failure(VehicleErrors.Disposed);
        }

        if (Status != VehicleStatus.Retired)
        {
            return Result.Failure(VehicleErrors.NotRetired);
        }

        if (salePriceCad is < 0)
        {
            return Result.Failure(VehicleErrors.InvalidSalePrice);
        }

        var previous = Status;
        Status = method == DisposalMethod.Sold ? VehicleStatus.Sold : VehicleStatus.Recycled;
        SalePriceCad = method == DisposalMethod.Sold ? salePriceCad : null;
        DisposedAtUtc = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
        {
            StatusReason = note!.Trim();
        }

        UpdatedAtUtc = DisposedAtUtc.Value;

        Raise(new VehicleStatusChangedDomainEvent(Id, previous, Status, StatusReason));
        Raise(new VehicleDisposedDomainEvent(Id, method, SalePriceCad));
        return Result.Success();
    }

    private static Result ValidateDetails(
        string unitNumber,
        int year,
        int seatingCapacity,
        int odometerKm,
        decimal acquisitionCostCad,
        int endOfLifeKm)
    {
        if (string.IsNullOrWhiteSpace(unitNumber))
        {
            return Result.Failure(VehicleErrors.InvalidUnitNumber);
        }

        if (year < 1900 || year > DateTimeOffset.UtcNow.Year + 1)
        {
            return Result.Failure(VehicleErrors.InvalidYear);
        }

        if (seatingCapacity <= 0)
        {
            return Result.Failure(VehicleErrors.InvalidCapacity);
        }

        if (odometerKm < 0)
        {
            return Result.Failure(VehicleErrors.InvalidOdometer);
        }

        if (acquisitionCostCad < 0)
        {
            return Result.Failure(VehicleErrors.InvalidAcquisitionCost);
        }

        if (endOfLifeKm <= 0)
        {
            return Result.Failure(VehicleErrors.InvalidEndOfLifeKm);
        }

        return Result.Success();
    }
}
