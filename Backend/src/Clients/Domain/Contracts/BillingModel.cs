namespace NorthernLink.Clients.Domain.Contracts;

/// <summary>
/// How a contract is invoiced. <see cref="RoundTripRate"/> contracts are auto-drafted by
/// Billing at <c>RatePerRoundTripCad</c> per completed round trip; <see cref="Manual"/>
/// contracts still land as billable trips but are invoiced via manual lines.
/// </summary>
public enum BillingModel
{
    RoundTripRate,
    Manual,
}
