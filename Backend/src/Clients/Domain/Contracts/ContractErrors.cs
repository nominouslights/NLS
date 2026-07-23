using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.Contracts;

/// <summary>All domain errors the Contract aggregate (and its handlers) can produce.</summary>
public static class ContractErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Clients.Contract.NotFound", "The contract was not found.");

    public static readonly Error InvalidPeriod = Error.Validation(
        "Clients.Contract.InvalidPeriod", "The contract end date cannot be before its start date.");

    public static readonly Error RateRequired = Error.Validation(
        "Clients.Contract.RateRequired", "A rate per round trip is required for round-trip-rate billing.");

    public static readonly Error InvalidRate = Error.Validation(
        "Clients.Contract.InvalidRate", "The rate per round trip must be greater than zero.");

    public static readonly Error RateNotAllowed = Error.Validation(
        "Clients.Contract.RateNotAllowed", "A rate per round trip only applies to round-trip-rate billing.");

    public static readonly Error InvalidNetTerms = Error.Validation(
        "Clients.Contract.InvalidNetTerms", "Net terms must be a positive number of days.");

    public static readonly Error NotActive = Error.Conflict(
        "Clients.Contract.NotActive", "Only an active contract can be changed.");

    public static readonly Error OverlappingPeriod = Error.Conflict(
        "Clients.Contract.OverlappingPeriod",
        "The client already has an active contract covering part of this period.");
}
