using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Vehicles;

public sealed class VehicleServiceRecord : Entity<Guid>
{
    public Guid VehicleId { get; private set; }
    public ServiceCategory ServiceCategory { get; private set; }

    /// <summary>Only populated when ServiceCategory == FluidChange.</summary>
    public FluidType? FluidType { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>True = scheduled/planned maintenance; false = unplanned/emergency repair.</summary>
    public bool IsPlanned { get; private set; }

    public ServiceStatus ServiceStatus { get; private set; }
    public ServicePriority Priority { get; private set; }

    public DateTime? ScheduledDate { get; private set; }
    public DateTime? StartedDate { get; private set; }
    public DateTime? CompletedDate { get; private set; }

    public int? OdometerAtService { get; private set; }
    public decimal? EstimatedCostDollars { get; private set; }
    public decimal? ActualCostDollars { get; private set; }

    public string? ServiceProvider { get; private set; }
    public string? TechnicianName { get; private set; }
    public string? PartsNotes { get; private set; }
    public bool IsWarrantyWork { get; private set; }

    public DateTime? NextServiceDueDateUtc { get; private set; }
    public int? NextServiceDueOdometerKm { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private VehicleServiceRecord() { }

    public static VehicleServiceRecord Create(
        Guid vehicleId,
        ServiceCategory serviceCategory,
        FluidType? fluidType,
        string title,
        string? description,
        bool isPlanned,
        ServiceStatus serviceStatus,
        ServicePriority priority,
        DateTime? scheduledDate,
        int? odometerAtService,
        decimal? estimatedCostDollars,
        string? serviceProvider,
        string? technicianName,
        string? partsNotes,
        bool isWarrantyWork,
        DateTime? nextServiceDueDateUtc,
        int? nextServiceDueOdometerKm)
    {
        return new VehicleServiceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceCategory = serviceCategory,
            FluidType = fluidType,
            Title = Guard.AgainstNullOrEmpty(title, nameof(title)),
            Description = description,
            IsPlanned = isPlanned,
            ServiceStatus = serviceStatus,
            Priority = priority,
            ScheduledDate = scheduledDate,
            OdometerAtService = odometerAtService,
            EstimatedCostDollars = estimatedCostDollars,
            ServiceProvider = serviceProvider,
            TechnicianName = technicianName,
            PartsNotes = partsNotes,
            IsWarrantyWork = isWarrantyWork,
            NextServiceDueDateUtc = nextServiceDueDateUtc,
            NextServiceDueOdometerKm = nextServiceDueOdometerKm,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        ServiceCategory serviceCategory,
        FluidType? fluidType,
        string title,
        string? description,
        bool isPlanned,
        ServiceStatus serviceStatus,
        ServicePriority priority,
        DateTime? scheduledDate,
        int? odometerAtService,
        decimal? estimatedCostDollars,
        string? serviceProvider,
        string? technicianName,
        string? partsNotes,
        bool isWarrantyWork,
        DateTime? nextServiceDueDateUtc,
        int? nextServiceDueOdometerKm)
    {
        ServiceCategory = serviceCategory;
        FluidType = fluidType;
        Title = Guard.AgainstNullOrEmpty(title, nameof(title));
        Description = description;
        IsPlanned = isPlanned;
        ServiceStatus = serviceStatus;
        Priority = priority;
        ScheduledDate = scheduledDate;
        OdometerAtService = odometerAtService;
        EstimatedCostDollars = estimatedCostDollars;
        ServiceProvider = serviceProvider;
        TechnicianName = technicianName;
        PartsNotes = partsNotes;
        IsWarrantyWork = isWarrantyWork;
        NextServiceDueDateUtc = nextServiceDueDateUtc;
        NextServiceDueOdometerKm = nextServiceDueOdometerKm;
    }

    /// <summary>Transitions the record to InProgress and records the start time.</summary>
    public void Start()
    {
        Guard.Against(ServiceStatus == ServiceStatus.Completed, "Cannot start a completed service record.");
        Guard.Against(ServiceStatus == ServiceStatus.Cancelled, "Cannot start a cancelled service record.");
        ServiceStatus = ServiceStatus.InProgress;
        StartedDate = DateTime.UtcNow;
    }

    /// <summary>Transitions the record to Completed.</summary>
    public void Complete(DateTime completedDate, decimal? actualCostDollars, int? odometerAtService)
    {
        Guard.Against(ServiceStatus == ServiceStatus.Cancelled, "Cannot complete a cancelled service record.");
        ServiceStatus = ServiceStatus.Completed;
        CompletedDate = completedDate;
        ActualCostDollars = actualCostDollars;
        if (odometerAtService.HasValue)
            OdometerAtService = odometerAtService;
    }
}
