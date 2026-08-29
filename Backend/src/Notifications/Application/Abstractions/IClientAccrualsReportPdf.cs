using NorthernLink.Notifications.Application.Dispatches;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>
/// Renders a <see cref="ClientAccrualsReport"/> into a PDF document (the file bytes). Mirrors
/// the <see cref="IPickupEmailReportPdf"/> shape — an Application-layer abstraction whose
/// concrete implementation lives in Infrastructure so this layer stays off the third-party
/// PDF dependency. Implementations should never throw for well-formed input and must tolerate
/// empty sections (an empty month is still a printable report).
/// </summary>
public interface IClientAccrualsReportPdf
{
    /// <summary>Returns the rendered PDF bytes. Never returns null.</summary>
    byte[] Build(ClientAccrualsReport report);
}
