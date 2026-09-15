using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Drivers;

/// <summary>All domain errors the Driver aggregate (and its handlers) can produce.</summary>
public static class DriverErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Drivers.Driver.NotFound", "The driver was not found.");

    public static readonly Error NameRequired = Error.Validation(
        "Drivers.Driver.NameRequired", "A driver name is required.");

    public static readonly Error LicenceClassRequired = Error.Validation(
        "Drivers.Driver.LicenceClassRequired", "A licence class is required.");

    public static readonly Error SourceRequired = Error.Validation(
        "Drivers.Driver.SourceRequired", "A driver source (Northern Link or the partner name) is required.");

    public static Error InvalidStatusTransition(DriverStatus from, DriverStatus to) => Error.Conflict(
        "Drivers.Driver.InvalidStatusTransition", $"A driver cannot move from {from} to {to}.");

    /// <summary>
    /// The signed-in account has no driver row. Returned as a 404 by
    /// <c>GET /api/drivers/me</c> and by <c>UnlinkDriverUser</c>.
    /// <para>
    /// The code is the bare <c>Drivers.NotLinked</c>, breaking the <c>Drivers.Driver.*</c>
    /// convention on purpose: it is a contract value the Driver Field App branches on to show
    /// "your account isn't linked to a driver record — ask dispatch" instead of a generic error,
    /// so it is pinned by a test and must not be renamed for tidiness.
    /// </para>
    /// </summary>
    public static readonly Error NotLinked = Error.NotFound(
        "Drivers.NotLinked", "This account is not linked to a driver record.");

    public static readonly Error UserIdRequired = Error.Validation(
        "Drivers.Driver.UserIdRequired", "A user id is required to link a driver to an account.");

    public static readonly Error AlreadyLinkedToAnotherUser = Error.Conflict(
        "Drivers.Driver.AlreadyLinkedToAnotherUser",
        "This driver is already linked to a different account. Unlink it first.");

    public static readonly Error UserAlreadyLinkedToAnotherDriver = Error.Conflict(
        "Drivers.Driver.UserAlreadyLinkedToAnotherDriver",
        "That account is already linked to a different driver. Unlink it first.");
}
