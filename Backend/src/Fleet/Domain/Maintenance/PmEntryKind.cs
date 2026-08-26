namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>
/// What a <see cref="PmCompletion"/> certifies: a routine maintenance item (PM-…)
/// or a major-component overhaul (OH-…).
/// </summary>
public enum PmEntryKind
{
    Item,
    Overhaul,
}
