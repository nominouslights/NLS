using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.Clients;

/// <summary>All domain errors the Client aggregate (and its handlers) can produce.</summary>
public static class ClientErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Clients.Client.NotFound", "The client was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Clients.Client.NameRequired", "A client name is required.");

    public static readonly Error TagRequired = Error.Validation(
        "Clients.Client.TagRequired", "A client tag is required.");

    public static readonly Error ServiceTypeRequired = Error.Validation(
        "Clients.Client.ServiceTypeRequired", "A service type is required for Client-type organizations.");

    public static readonly Error ServiceTypeNotAllowed = Error.Validation(
        "Clients.Client.ServiceTypeNotAllowed", "A service type does not apply to Vendor/Partner organizations.");
}
