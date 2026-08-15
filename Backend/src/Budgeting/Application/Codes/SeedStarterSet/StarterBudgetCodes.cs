using NorthernLink.Budgeting.Domain.Codes;

namespace NorthernLink.Budgeting.Application.Codes.SeedStarterSet;

/// <summary>
/// The starter chart of accounts a tenant can begin from — a proposal, not scripture. Every row
/// is an ordinary budget code once created: editable, retirable, deletable while unused.
/// <para>
/// <b>Flat by design.</b> No row names a parent, so seeding is one pass with no ordering
/// constraint and no risk of a half-built hierarchy if something fails midway. A planner adds
/// rollups afterwards if they want them.
/// </para>
/// <para>
/// The six revenue codes cover one service line each, which is what makes architecture §5.3's
/// Rider Express revenue-mix comparison (roughly 70-75% passenger / 15-20% parcel-freight /
/// 5-10% charter / 2-5% ancillary) computable from day one rather than after someone hand-builds
/// a chart. The expense codes cover the cost categories the rest of the platform already tracks:
/// fuel, maintenance and insurance against the fleet, wages against administration, and the
/// apprenticeship program §5.3 names as its own stream.
/// </para>
/// </summary>
public static class StarterBudgetCodes
{
    /// <summary>One proposed code. Description doubles as the explanation shown before seeding.</summary>
    public sealed record Seed(
        string Code,
        string Name,
        BudgetCodeCategory Category,
        BudgetServiceLine ServiceLine,
        string Description);

    public static readonly IReadOnlyList<Seed> All =
    [
        // Revenue — one per service line, so revenue mix is readable without further setup.
        new("ZBB-CREW-01", "Mine crew shuttle", BudgetCodeCategory.Revenue, BudgetServiceLine.ContractCrew,
            "Contracted crew rotation runs under the mine-site master agreement."),
        new("ZBB-COMM-01", "Community passenger", BudgetCodeCategory.Revenue, BudgetServiceLine.Community,
            "Demand-activated community service at a fixed seat fare."),
        new("ZBB-NIHB-01", "NIHB medical transport", BudgetCodeCategory.Revenue, BudgetServiceLine.Nihb,
            "Direct-billed medical transport under the NIHB benefit."),
        new("ZBB-CHTR-01", "Charter", BudgetCodeCategory.Revenue, BudgetServiceLine.Charter,
            "Ad-hoc charters. Only booked-and-confirmed charters enter a period plan."),
        new("ZBB-CARGO-01", "Parcel and cargo", BudgetCodeCategory.Revenue, BudgetServiceLine.Cargo,
            "Freight and parcel carried on scheduled and dedicated runs."),
        new("ZBB-GROC-01", "Grocery run", BudgetCodeCategory.Revenue, BudgetServiceLine.Grocery,
            "The weekly grocery run, including pickup-hub handling."),

        // Expense — the cost categories the rest of the platform already generates data for.
        new("ZBB-FUEL-01", "Fuel", BudgetCodeCategory.Expense, BudgetServiceLine.Fleet,
            "Largest single variable cost. Built from planned corridor kilometres."),
        new("ZBB-MAINT-01", "Fleet maintenance and parts", BudgetCodeCategory.Expense, BudgetServiceLine.Fleet,
            "Scheduled preventive maintenance plus a parts float sized to the open work-order backlog."),
        new("ZBB-INSUR-01", "Insurance and licensing", BudgetCodeCategory.Expense, BudgetServiceLine.Fleet,
            "Fleet insurance premiums, plates and operating authorities."),
        new("ZBB-WAGE-01", "Driver wages and benefits", BudgetCodeCategory.Expense, BudgetServiceLine.Administrative,
            "Driven by the rotation roster and hours-of-service-legal shift coverage."),
        new("ZBB-ADMIN-01", "Administration and overhead", BudgetCodeCategory.Expense, BudgetServiceLine.Administrative,
            "Office, software, professional fees and everything not attributable to a service line."),
        new("ZBB-APPR-01", "Apprenticeship program", BudgetCodeCategory.Expense, BudgetServiceLine.Apprenticeship,
            "Training, mentoring hours and certification costs for the apprenticeship stream."),
    ];
}
