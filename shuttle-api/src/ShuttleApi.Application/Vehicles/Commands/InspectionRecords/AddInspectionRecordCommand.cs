using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.InspectionRecords;

public sealed record AddInspectionRecordCommand(
    Guid VehicleId,
    InspectionType InspectionType,
    DateTime InspectedAt,
    DateTime? ExpiresAt,
    string? InspectorName,
    string? InspectionFacility,
    string? CertificateNumber,
    InspectionResult InspectionResult,
    string? DeficienciesNotes,
    string? CorrectiveActionNotes,
    decimal? CostDollars) : ICommand<AddInspectionRecordResult>;

public sealed record AddInspectionRecordResult(Guid RecordId);
