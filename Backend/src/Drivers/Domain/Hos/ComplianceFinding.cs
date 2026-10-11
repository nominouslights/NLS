using NorthernLink.Drivers.Domain.Hos.Events;
using NorthernLink.Shared.HoursOfService;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Hos;

/// <summary>
/// A compliance officer's finding against a driver's record for one day: which rule
/// (<see cref="Rule"/> — a rule engine <see cref="HosRule"/> name, or one of the four
/// record-keeping offences the engine cannot see: <c>Falsification</c>, <c>Tampering</c>,
/// <c>TwoRecords</c>, <c>OutOfServiceDeclared</c>), how severe, and how it was worked
/// (<see cref="FindingStatus"/>: Open → Corrected → Closed). Findings are never deleted — a
/// closed one stays on the audit — and Closed is terminal. Opened by a compliance officer from
/// an audit or an enforcement contact, or by the platform itself when an out-of-service
/// declaration is recorded (<see cref="FindingSource.Auto"/>).
/// </summary>
public sealed class ComplianceFinding : AggregateRoot, ITenantScoped
{
    public const int RuleMaxLength = 64;
    public const int DescriptionMaxLength = 1000;
    public const int FollowUpNoteMaxLength = 2000;

    /// <summary>The record-keeping offences a finding may cite beyond the engine's rules.</summary>
    public static readonly string[] RecordKeepingRules = ["Falsification", "Tampering", "TwoRecords", "OutOfServiceDeclared"];

    public const string OutOfServiceDeclaredRule = "OutOfServiceDeclared";

    private ComplianceFinding()
    {
        // EF Core materialization only.
        Rule = null!;
        Description = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }

    /// <summary>The America/Winnipeg day the finding is against.</summary>
    public DateOnly LocalDate { get; private set; }

    public string Rule { get; private set; }
    public FindingSeverity Severity { get; private set; }
    public FindingStatus Status { get; private set; }
    public string Description { get; private set; }
    public string? FollowUpNote { get; private set; }
    public FindingSource Source { get; private set; }
    public DateTimeOffset OpenedAtUtc { get; private set; }
    public Guid OpenedByUserId { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>True for a rule the engine can report or one of <see cref="RecordKeepingRules"/>.</summary>
    public static bool IsKnownRule(string? rule) =>
        !string.IsNullOrWhiteSpace(rule)
        && (HosWire.RuleFrom(rule.Trim()) is not null || Array.IndexOf(RecordKeepingRules, rule.Trim()) >= 0);

    public static Result<ComplianceFinding> Open(
        Guid tenantId,
        Guid driverId,
        DateOnly localDate,
        string? rule,
        FindingSeverity severity,
        string? description,
        FindingSource source,
        Guid openedByUserId,
        DateTimeOffset now,
        string? followUpNote = null)
    {
        if (!IsKnownRule(rule))
        {
            return Result.Failure<ComplianceFinding>(ComplianceErrors.FindingRuleInvalid);
        }

        if (!Enum.IsDefined(severity))
        {
            return Result.Failure<ComplianceFinding>(ComplianceErrors.FindingSeverityInvalid);
        }

        if (!Enum.IsDefined(source))
        {
            return Result.Failure<ComplianceFinding>(ComplianceErrors.FindingSourceInvalid);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Failure<ComplianceFinding>(ComplianceErrors.FindingDescriptionRequired);
        }

        var finding = new ComplianceFinding
        {
            TenantId = tenantId,
            DriverId = driverId,
            LocalDate = localDate,
            Rule = rule!.Trim(),
            Severity = severity,
            Status = FindingStatus.Open,
            Description = Truncate(description.Trim(), DescriptionMaxLength),
            FollowUpNote = CleanNote(followUpNote),
            Source = source,
            OpenedAtUtc = now,
            OpenedByUserId = openedByUserId,
            UpdatedAtUtc = now,
        };

        finding.Raise(new ComplianceFindingOpenedDomainEvent(finding.Id, driverId, tenantId, finding.Rule, severity));
        return Result.Success(finding);
    }

    /// <summary>
    /// Moves the finding along its workflow and/or updates the follow-up note. Same status with
    /// the same note is a no-op (no event, no write). Closed is terminal.
    /// </summary>
    public Result Update(FindingStatus status, string? followUpNote, Guid byUserId, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status))
        {
            return Result.Failure(ComplianceErrors.FindingStatusInvalid);
        }

        var note = CleanNote(followUpNote);
        if (status == Status)
        {
            if (string.Equals(note, FollowUpNote, StringComparison.Ordinal))
            {
                return Result.Success();
            }

            if (Status == FindingStatus.Closed)
            {
                return Result.Failure(ComplianceErrors.FindingClosed);
            }
        }
        else
        {
            if (Status == FindingStatus.Closed)
            {
                return Result.Failure(ComplianceErrors.FindingClosed);
            }

            var forward = (Status, status) switch
            {
                (FindingStatus.Open, FindingStatus.Corrected or FindingStatus.Closed) => true,
                (FindingStatus.Corrected, FindingStatus.Closed) => true,
                _ => false,
            };

            if (!forward)
            {
                return Result.Failure(ComplianceErrors.FindingTransitionInvalid);
            }

            Status = status;
            if (status == FindingStatus.Closed)
            {
                ClosedAtUtc = now;
                ClosedByUserId = byUserId;
            }
        }

        FollowUpNote = note;
        UpdatedAtUtc = now;

        Raise(new ComplianceFindingUpdatedDomainEvent(Id, DriverId, TenantId, Status));
        return Result.Success();
    }

    private static string? CleanNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : Truncate(note.Trim(), FollowUpNoteMaxLength);

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
