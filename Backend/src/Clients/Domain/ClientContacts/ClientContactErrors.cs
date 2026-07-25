using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.ClientContacts;

/// <summary>All domain errors the ClientContact aggregate can produce.</summary>
public static class ClientContactErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Clients.ClientContact.NotFound", "The contact was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Clients.ClientContact.NameRequired", "A contact name is required.");

    public static readonly Error TitleRequired = Error.Validation(
        "Clients.ClientContact.TitleRequired", "A contact title/role is required.");

    public static readonly Error PrimaryAlreadyExists = Error.Conflict(
        "Clients.ClientContact.PrimaryAlreadyExists", "This client already has a primary contact.");
}
