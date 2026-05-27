using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

public sealed record UpdateServiceRecordCommand(
    Guid VehicleId,
    Guid RecordId,
    ServiceCategory ServiceCategory,
    FluidType? FluidType,
    string Title,
    string? Description,
    bool IsPlanned,
    ServiceStatus ServiceStatus,
    ServicePriority Priority,
    DateTime? ScheduledDate,
    int? OdometerAtService,
    decimal? EstimatedCostDollars,
    string? ServiceProvider,
    string? TechnicianName,
    string? PartsNotes,
    bool IsWarrantyWork,
    DateTime? NextServiceDueDateUtc,
    int? NextServiceDueOdometerKm) : ICommand;
