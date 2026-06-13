using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Vehicles;

public sealed class Vehicle : AggregateRoot<Guid>
{
    private readonly List<VehicleServiceRecord> _serviceRecords = [];
    private readonly List<VehicleInspectionRecord> _inspectionRecords = [];
    private readonly List<VehicleFuelEntry> _fuelEntries = [];

    /// <summary>
    /// Human-readable fleet identifier assigned by operations (e.g. NL-001, NL-002).
    /// Must be unique across the fleet.
    /// </summary>
    public string UnitCode { get; private set; } = string.Empty;

    public string VIN { get; private set; } = string.Empty;
    public string Make { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public int Year { get; private set; }
    public string Color { get; private set; } = string.Empty;
    public string LicensePlate { get; private set; } = string.Empty;
    public string Province { get; private set; } = string.Empty;
    public VehicleType VehicleType { get; private set; }
    public int PassengerCapacity { get; private set; }
    public int CurrentOdometerKm { get; private set; }
    public DateTime AcquisitionDate { get; private set; }

    public DateTime? RegistrationExpiry { get; private set; }

    public string? InsuranceProvider { get; private set; }
    public string? InsurancePolicyNumber { get; private set; }
    public DateTime? InsuranceExpiry { get; private set; }

    public VehicleStatus Status { get; private set; }

    /// <summary>
    /// Free-text note explaining the current status. Required when Status is OutOfService.
    /// Cleared when transitioning back to Active.
    /// </summary>
    public string? StatusNote { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? Notes { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    public IReadOnlyList<VehicleServiceRecord> ServiceRecords => _serviceRecords.AsReadOnly();
    public IReadOnlyList<VehicleInspectionRecord> InspectionRecords => _inspectionRecords.AsReadOnly();
    public IReadOnlyList<VehicleFuelEntry> FuelEntries => _fuelEntries.AsReadOnly();

    // ── Computed helpers (not persisted) ──────────────────────────────────────
    public bool IsRegistrationExpiringSoon =>
        RegistrationExpiry.HasValue && RegistrationExpiry.Value <= DateTime.UtcNow.AddDays(30);

    public bool IsInsuranceExpiringSoon =>
        InsuranceExpiry.HasValue && InsuranceExpiry.Value <= DateTime.UtcNow.AddDays(30);

    private Vehicle() { }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Vehicle Create(
        string unitCode,
        string vin,
        string make,
        string model,
        int year,
        string color,
        string licensePlate,
        string province,
        VehicleType vehicleType,
        int passengerCapacity,
        int currentOdometerKm,
        DateTime acquisitionDate,
        DateTime? registrationExpiry,
        string? insuranceProvider,
        string? insurancePolicyNumber,
        DateTime? insuranceExpiry,
        string? notes)
    {
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            UnitCode = Guard.AgainstNullOrEmpty(unitCode, nameof(unitCode)),
            VIN = Guard.AgainstNullOrEmpty(vin, nameof(vin)),
            Make = Guard.AgainstNullOrEmpty(make, nameof(make)),
            Model = Guard.AgainstNullOrEmpty(model, nameof(model)),
            Year = year,
            Color = Guard.AgainstNullOrEmpty(color, nameof(color)),
            LicensePlate = Guard.AgainstNullOrEmpty(licensePlate, nameof(licensePlate)),
            Province = Guard.AgainstNullOrEmpty(province, nameof(province)),
            VehicleType = vehicleType,
            PassengerCapacity = passengerCapacity,
            CurrentOdometerKm = currentOdometerKm,
            AcquisitionDate = acquisitionDate,
            RegistrationExpiry = registrationExpiry,
            InsuranceProvider = insuranceProvider,
            InsurancePolicyNumber = insurancePolicyNumber,
            InsuranceExpiry = insuranceExpiry,
            Status = VehicleStatus.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Notes = notes
        };
    }

    // ── Mutations ─────────────────────────────────────────────────────────────
    public void Update(
        string unitCode,
        string vin,
        string make,
        string model,
        int year,
        string color,
        string licensePlate,
        string province,
        VehicleType vehicleType,
        int passengerCapacity,
        int currentOdometerKm,
        DateTime acquisitionDate,
        DateTime? registrationExpiry,
        string? insuranceProvider,
        string? insurancePolicyNumber,
        DateTime? insuranceExpiry,
        bool isActive,
        string? notes)
    {
        UnitCode = Guard.AgainstNullOrEmpty(unitCode, nameof(unitCode));
        VIN = Guard.AgainstNullOrEmpty(vin, nameof(vin));
        Make = Guard.AgainstNullOrEmpty(make, nameof(make));
        Model = Guard.AgainstNullOrEmpty(model, nameof(model));
        Year = year;
        Color = Guard.AgainstNullOrEmpty(color, nameof(color));
        LicensePlate = Guard.AgainstNullOrEmpty(licensePlate, nameof(licensePlate));
        Province = Guard.AgainstNullOrEmpty(province, nameof(province));
        VehicleType = vehicleType;
        PassengerCapacity = passengerCapacity;
        CurrentOdometerKm = currentOdometerKm;
        AcquisitionDate = acquisitionDate;
        RegistrationExpiry = registrationExpiry;
        InsuranceProvider = insuranceProvider;
        InsurancePolicyNumber = insurancePolicyNumber;
        InsuranceExpiry = insuranceExpiry;
        IsActive = isActive;
        Notes = notes;
    }

    /// <summary>Generic status change for Active, InMaintenance, and Retired transitions.</summary>
    public void SetStatus(VehicleStatus status, string? statusNote = null)
    {
        Guard.Against(status == VehicleStatus.OutOfService,
            "Use SetOutOfService(reason) to mark a vehicle out of service.");
        Status = status;
        StatusNote = statusNote;
    }

    /// <summary>
    /// Marks the vehicle out of service. A non-empty reason is required
    /// so that dispatchers and mechanics know why the unit is unavailable.
    /// </summary>
    public void SetOutOfService(string reason)
    {
        Guard.AgainstNullOrEmpty(reason, nameof(reason));
        Status = VehicleStatus.OutOfService;
        StatusNote = reason;
    }

    public void UpdateOdometer(int newKm)
    {
        Guard.Against(newKm < CurrentOdometerKm,
            "New odometer reading cannot be less than the current reading.");
        CurrentOdometerKm = newKm;
    }

    // ── Child entity management ───────────────────────────────────────────────
    public void AddServiceRecord(VehicleServiceRecord record) =>
        _serviceRecords.Add(record);

    public void RemoveServiceRecord(Guid recordId)
    {
        var record = _serviceRecords.FirstOrDefault(r => r.Id == recordId);
        if (record is not null)
            _serviceRecords.Remove(record);
    }

    public void AddInspectionRecord(VehicleInspectionRecord record) =>
        _inspectionRecords.Add(record);

    public void RemoveInspectionRecord(Guid recordId)
    {
        var record = _inspectionRecords.FirstOrDefault(r => r.Id == recordId);
        if (record is not null)
            _inspectionRecords.Remove(record);
    }

    public void AddFuelEntry(VehicleFuelEntry entry) =>
        _fuelEntries.Add(entry);

    public void RemoveFuelEntry(Guid entryId)
    {
        var entry = _fuelEntries.FirstOrDefault(e => e.Id == entryId);
        if (entry is not null)
            _fuelEntries.Remove(entry);
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    public void Restore()
    {
        Guard.Against(!IsDeleted, "Vehicle is not archived.");
        IsDeleted = false;
        DeletedAt = null;
    }
}
