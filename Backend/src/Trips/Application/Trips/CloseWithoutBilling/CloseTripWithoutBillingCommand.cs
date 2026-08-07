using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.Trips.CloseWithoutBilling;

/// <summary>
/// Closes out a ReadyForBilling trip that will never be invoiced — a client with no active
/// contract, a goodwill run, a job written off before any worksheet existed. Without it
/// ReadyForBilling has no exit, since every other way out of it waits on an invoice that is
/// never going to be drafted. The reason is required: it is the only thing distinguishing this
/// from an invoice write-off in the audit trail.
/// </summary>
public sealed record CloseTripWithoutBillingCommand(Guid TripId, string Reason) : ICommand;
