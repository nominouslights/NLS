using NorthernLink.Notifications.Application.Dispatches;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>
/// Renders a <see cref="PickupEmailReport"/> into a PDF document (the file bytes). Mirrors the
/// <see cref="IEmailHtmlSanitizer"/> shape — an Application-layer abstraction whose concrete
/// implementation lives in Infrastructure so this layer stays off the third-party PDF dependency.
/// The report step is best-effort: implementations should never throw for well-formed input and
/// must tolerate empty body text.
/// </summary>
public interface IPickupEmailReportPdf
{
    /// <summary>Returns the rendered PDF bytes. Never returns null.</summary>
    byte[] Build(PickupEmailReport report);
}
