namespace NorthernLink.Shared.HoursOfService;

/// <summary>
/// The strings the API emits and accepts for HOS enums. The Dispatch Console and the Driver Field
/// App key on these exact values; the Drivers module's <c>HosDisplay</c> delegates here. Violation
/// ids on the wire are <see cref="HosRule"/> member names verbatim (<see cref="RuleId"/>).
/// </summary>
public static class HosWire
{
    public const string OffDuty = "Off Duty";
    public const string OnDuty = "On Duty";
    public const string Driving = "Driving";
    public const string PersonalConveyance = "Personal Conveyance";

    public const string DriverAppSource = "Driver App";
    public const string EldTranscriptionSource = "ELD transcription";

    /// <summary>Legacy string for <see cref="EventSource.PaperLog"/>; kept verbatim.</summary>
    public const string PaperLogSource = "Manual (paper backup)";

    public const string OtherCarrierSource = "Other carrier (declared)";
    public const string SystemSource = "System";

    public const string AdverseDrivingConditionsKind = "Adverse driving conditions";
    public const string EmergencyKind = "Emergency";
    public const string OffDutyDeferralKind = "Off-duty deferral (CO approved)";

    public const string OutOfServiceLabel = "Out of service";

    public static string Status(DutyStatus status) => status switch
    {
        DutyStatus.OffDuty => OffDuty,
        DutyStatus.OnDuty => OnDuty,
        DutyStatus.Driving => Driving,
        DutyStatus.PersonalConveyance => PersonalConveyance,
        _ => status.ToString(),
    };

    public static DutyStatus? StatusFrom(string? wire) => wire?.Trim() switch
    {
        OffDuty => DutyStatus.OffDuty,
        OnDuty => DutyStatus.OnDuty,
        Driving => DutyStatus.Driving,
        PersonalConveyance => DutyStatus.PersonalConveyance,
        _ => null,
    };

    public static string Source(EventSource source) => source switch
    {
        EventSource.DriverApp => DriverAppSource,
        EventSource.EldTranscription => EldTranscriptionSource,
        EventSource.PaperLog => PaperLogSource,
        EventSource.OtherCarrier => OtherCarrierSource,
        EventSource.System => SystemSource,
        _ => source.ToString(),
    };

    public static EventSource? SourceFrom(string? wire) => wire?.Trim() switch
    {
        DriverAppSource => EventSource.DriverApp,
        EldTranscriptionSource => EventSource.EldTranscription,
        PaperLogSource => EventSource.PaperLog,
        OtherCarrierSource => EventSource.OtherCarrier,
        SystemSource => EventSource.System,
        _ => null,
    };

    public static string ExceptionKind(HosException exception) => exception switch
    {
        AdverseDrivingConditions => AdverseDrivingConditionsKind,
        Emergency => EmergencyKind,
        OffDutyDeferral => OffDutyDeferralKind,
        _ => exception.GetType().Name,
    };

    public static string RuleId(HosRule rule) => rule.ToString();

    public static HosRule? RuleFrom(string? id) =>
        Enum.TryParse<HosRule>(id, ignoreCase: false, out var rule) && Enum.IsDefined(rule) ? rule : null;
}
