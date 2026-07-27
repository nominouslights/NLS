using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.ClientInteractions;

/// <summary>All domain errors the ClientInteraction aggregate (and its handlers) can produce.</summary>
public static class ClientInteractionErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Clients.ClientInteraction.NotFound", "The interaction was not found.");

    public static readonly Error SummaryRequired = Error.Validation(
        "Clients.ClientInteraction.SummaryRequired", "An interaction summary is required.");
}
