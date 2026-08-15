namespace NorthernLink.Budgeting.Domain.Codes;

/// <summary>
/// Which line of business a budget code belongs to. Declared per-module and never shared —
/// integration events carry the value as a string, and Trips, Clients and Notifications each
/// declare their own equivalent (the <c>TripServiceType</c> convention).
/// <para>
/// <b>The first six members are byte-identical to
/// <c>NorthernLink.Trips.Domain.Trips.TripServiceType</c>, and that is load-bearing.</b> They are
/// stored via <c>HasConversion&lt;string&gt;()</c>, so the strings written here are the same
/// strings Trips and Billing already emit on <c>TripCompletedIntegrationEvent</c> and store on
/// <c>billable_trips.service_type</c>. Revenue-mix reporting therefore joins directly on the
/// value instead of needing a translation table — the "second taxonomy" this field exists to
/// avoid. Renaming one of the six here, or spelling it differently ("NIHB" for "Nihb"), silently
/// drops a whole revenue category from that report with no error anywhere.
/// </para>
/// <para>
/// The last three have no counterpart in <c>TripServiceType</c> and never will: a trip cannot be
/// "Administrative". They are the overhead side of the chart, and they are the reason this is a
/// separate enum rather than a reuse of the trip discriminator — extending
/// <c>TripServiceType</c> with them would corrupt a taxonomy four modules key off.
/// </para>
/// </summary>
public enum BudgetServiceLine
{
    // Revenue-generating service lines — keep in step with TripServiceType.
    ContractCrew,
    Community,
    Nihb,
    Charter,
    Cargo,
    Grocery,

    // Overhead. Budgeting-only: nothing else on the platform classifies work this way.
    Fleet,
    Administrative,
    Apprenticeship,
}
