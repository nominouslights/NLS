namespace NorthernLink.Identity.Application.Auth.Setup;

/// <summary>
/// First-run setup state. <c>SetupRequired</c> is true only while no user exists — the
/// Dispatch Console shows the "create administrator" screen when it is.
/// </summary>
public sealed record SetupStatusResponse(bool SetupRequired);
