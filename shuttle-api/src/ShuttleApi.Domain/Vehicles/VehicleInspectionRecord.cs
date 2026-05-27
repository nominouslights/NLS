using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Vehicles;

public sealed class VehicleInspectionRecord : Entity<Guid>
{
    public Guid VehicleId { get; private set; }
    public InspectionType InspectionType { get; private set; }

    public DateTime InspectedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    public string? InspectorName { get; private set; }
    public string? InspectionFacility { get; private set; }
    public string? CertificateNumber { get; private set; }

    public InspectionResult InspectionResult { get; private set; }

    public string? DeficienciesNotes { get; private set; }
    public string? CorrectiveActionNotes { get; private set; }

    public decimal? CostDollars { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public bool IsExpiringSoon =>
        ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow.AddDays(60);

    private VehicleInspectionRecord() { }

    public static VehicleInspectionRecord Create(
        Guid vehicleId,
        InspectionType inspectionType,
        DateTime inspectedAt,
        DateTime? expiresAt,
        string? inspectorName,
        string? inspectionFacility,
        string? certificateNumber,
        InspectionResult inspectionResult,
        string? deficienciesNotes,
        string? correctiveActionNotes,
        decimal? costDollars)
    {
        return new VehicleInspectionRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            InspectionType = inspectionType,
            InspectedAt = inspectedAt,
            ExpiresAt = expiresAt,
            InspectorName = inspectorName,
            InspectionFacility = inspectionFacility,
            CertificateNumber = certificateNumber,
            InspectionResult = inspectionResult,
            DeficienciesNotes = deficienciesNotes,
            CorrectiveActionNotes = correctiveActionNotes,
            CostDollars = costDollars,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        InspectionType inspectionType,
        DateTime inspectedAt,
        DateTime? expiresAt,
        string? inspectorName,
        string? inspectionFacility,
        string? certificateNumber,
        InspectionResult inspectionResult,
        string? deficienciesNotes,
        string? correctiveActionNotes,
        decimal? costDollars)
    {
        InspectionType = inspectionType;
        InspectedAt = inspectedAt;
        ExpiresAt = expiresAt;
        InspectorName = inspectorName;
        InspectionFacility = inspectionFacility;
        CertificateNumber = certificateNumber;
        InspectionResult = inspectionResult;
        DeficienciesNotes = deficienciesNotes;
        CorrectiveActionNotes = correctiveActionNotes;
        CostDollars = costDollars;
    }
}
