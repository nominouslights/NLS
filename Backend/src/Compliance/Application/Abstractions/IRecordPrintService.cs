using NorthernLink.Compliance.Application.Printing;

namespace NorthernLink.Compliance.Application.Abstractions;

/// <summary>
/// Renders a <see cref="RecordPrintDocument"/> to PDF bytes inside the shared page frame (carrier
/// header, record footer with page x of y). Templates are selected by
/// <see cref="RecordPrintDocument.TemplateKey"/>; <c>record-cover</c> is the first and the
/// fallback. Implementations are stateless renderers (QuestPDF in Infrastructure).
/// </summary>
public interface IRecordPrintService
{
    byte[] Render(RecordPrintDocument document);
}
