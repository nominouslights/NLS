using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.ServiceRecords;

public sealed record AddServiceRecordCommand(
    Guid VehicleId,
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
    int? NextServiceDueOdometerKm) : ICommand<AddServiceRecordResult>;

public sealed record AddServiceRecordResult(Guid RecordId);
