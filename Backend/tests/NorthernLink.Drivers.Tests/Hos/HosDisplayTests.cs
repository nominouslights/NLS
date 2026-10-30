using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Domain.Hos;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;
using Xunit;
using DutyStatus = NorthernLink.Drivers.Domain.Hos.DutyStatus;

namespace NorthernLink.Drivers.Tests.Hos;

/// <summary>
/// The wire vocabulary is <c>HosWire</c>'s: every string the ledger emits is one it accepts,
/// the legacy strings are unchanged, and the Drivers enums map onto the engine's by name.
/// </summary>
public class HosDisplayTests
{
    [Theory]
    [InlineData(DutyStatus.OffDuty, "Off Duty")]
    [InlineData(DutyStatus.OnDuty, "On Duty")]
    [InlineData(DutyStatus.Driving, "Driving")]
    [InlineData(DutyStatus.PersonalConveyance, "Personal Conveyance")]
    public void Every_duty_status_round_trips_through_its_wire_string(DutyStatus status, string wire)
    {
        Assert.Equal(wire, HosDisplay.DutyToWire(status));

        var parsed = HosDisplay.DutyFromWire(wire);
        Assert.True(parsed.IsSuccess);
        Assert.Equal(status, parsed.Value);
    }

    [Theory]
    [InlineData(DutyEventSource.DriverApp, "Driver App")]
    [InlineData(DutyEventSource.EldTranscription, "ELD transcription")]
    [InlineData(DutyEventSource.PaperLog, "Manual (paper backup)")]
    [InlineData(DutyEventSource.OtherCarrier, "Other carrier (declared)")]
    [InlineData(DutyEventSource.System, "System")]
    public void Every_event_source_round_trips_through_its_wire_string(DutyEventSource source, string wire)
    {
        Assert.Equal(wire, HosDisplay.SourceToWire(source));

        var parsed = HosDisplay.SourceFromWire(wire);
        Assert.True(parsed.IsSuccess);
        Assert.Equal(source, parsed.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void An_absent_event_source_means_paper_log(string? value)
    {
        var parsed = HosDisplay.SourceFromWire(value);

        Assert.True(parsed.IsSuccess);
        Assert.Equal(DutyEventSource.PaperLog, parsed.Value);
    }

    [Theory]
    [InlineData("Personal conveyance")]
    [InlineData("PersonalConveyance")]
    [InlineData("Sleeper")]
    public void An_unknown_duty_string_is_a_400_listing_the_valid_values(string value)
    {
        var parsed = HosDisplay.DutyFromWire(value);

        Assert.True(parsed.IsFailure);
        Assert.Equal(HosErrors.InvalidDuty, parsed.Error);
        Assert.Equal(ErrorType.Validation, parsed.Error.Type);
        Assert.Contains("Personal Conveyance", parsed.Error.Message);
    }

    [Theory]
    [InlineData("ELD")]
    [InlineData("eld transcription")]
    public void An_unknown_source_string_is_a_400_listing_the_valid_values(string value)
    {
        var parsed = HosDisplay.SourceFromWire(value);

        Assert.True(parsed.IsFailure);
        Assert.Equal(HosErrors.InvalidSource, parsed.Error);
        Assert.Contains("ELD transcription", parsed.Error.Message);
    }

    [Fact]
    public void The_legacy_paper_backup_string_is_the_engines_paper_log_string()
    {
        Assert.Equal(HosWire.PaperLogSource, HosDisplay.SourceToWire(HosLogEntrySource.ManualPaperBackup));
        Assert.Equal(HosWire.PaperLogSource, HosDisplay.SourceToWire(DutyEventSource.PaperLog));
        Assert.Equal(HosWire.DriverAppSource, HosDisplay.SourceToWire(HosLogEntrySource.DriverApp));
    }

    [Fact]
    public void The_drivers_enums_are_spelled_like_the_engines()
    {
        Assert.Equal(Enum.GetNames<DutyStatus>(), Enum.GetNames<NorthernLink.Shared.HoursOfService.DutyStatus>());
        Assert.Equal(Enum.GetNames<DutyEventSource>(), Enum.GetNames<EventSource>());

        foreach (var status in Enum.GetValues<DutyStatus>())
        {
            Assert.Equal(status, HosDisplay.FromEngine(HosDisplay.ToEngine(status)));
        }

        foreach (var source in Enum.GetValues<DutyEventSource>())
        {
            Assert.Equal(source, HosDisplay.FromEngine(HosDisplay.ToEngine(source)));
        }
    }

    [Fact]
    public void The_planning_label_names_the_eld_as_the_record()
    {
        Assert.Equal(
            "Planning figures from the app ledger — not the record of duty status (the ELD is the RODS)",
            HosDisplay.PlanningLabel);
    }
}
