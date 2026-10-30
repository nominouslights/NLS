using NorthernLink.Shared.Messaging;

namespace NorthernLink.Trips.Application.BookeoImports.Preview;

/// <summary>
/// Parses an uploaded Bookeo booking report and plans what importing it would do — writing
/// nothing but the batch row (the normalized, tax-free rows and the plan hash) so a later commit
/// can recompute and apply exactly this plan. <paramref name="Content"/> is the raw file; it is
/// read once and never stored. <paramref name="UploadedBy"/> comes from the signed token.
/// </summary>
public sealed record PreviewBookeoImportCommand(
    Guid TenantId,
    string FileName,
    byte[] Content,
    string UploadedBy) : ICommand<BookeoImportPreview>;
