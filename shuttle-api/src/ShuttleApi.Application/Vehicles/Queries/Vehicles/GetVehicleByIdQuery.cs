using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Queries.Vehicles;

public sealed record GetVehicleByIdQuery(Guid Id) : IQuery<VehicleDetailResult>;

public sealed record VehicleDetailResult(
    Guid Id,
    string UnitCode,
    string VIN,
    string Make,
    string Model,
    int Year,
    string Color,
    string LicensePlate,
    string Province,
    string VehicleType,
    int PassengerCapacity,
    int CurrentOdometerKm,
    DateTime AcquisitionDate,
    DateTime? RegistrationExpiry,
    string? InsuranceProvider,
    string? InsurancePolicyNumber,
    DateTime? InsuranceExpiry,
    string Status,
    string? StatusNote,
    bool IsActive,
    DateTime CreatedAt,
    string? Notes,
    int ReadinessScore,
    IReadOnlyList<string> Alerts,
    bool IsRegistrationExpiringSoon,
    bool IsInsuranceExpiringSoon,
    IReadOnlyList<ServiceRecordResult> ServiceRecords,
    IReadOnlyList<InspectionRecordResult> InspectionRecords);

public sealed record ServiceRecordResult(
    Guid Id,
    string ServiceCategory,
    string? FluidType,
    string Title,
    string? Description,
    bool IsPlanned,
    string ServiceStatus,
    string Priority,
    DateTime? ScheduledDate,
    DateTime? StartedDate,
    DateTime? CompletedDate,
    int? OdometerAtService,
    decimal? EstimatedCostDollars,
    decimal? ActualCostDollars,
    string? ServiceProvider,
    string? TechnicianName,
    string? PartsNotes,
    bool IsWarrantyWork,
    DateTime? NextServiceDueDateUtc,
    int? NextServiceDueOdometerKm,
    DateTime CreatedAt);

public sealed record InspectionRecordResult(
    Guid Id,
    string InspectionType,
    DateTime InspectedAt,
    DateTime? ExpiresAt,
    string? InspectorName,
    string? InspectionFacility,
    string? CertificateNumber,
    string InspectionResult,
    string? DeficienciesNotes,
    string? CorrectiveActionNotes,
    decimal? CostDollars,
    DateTime CreatedAt,
    bool IsExpiringSoon);
