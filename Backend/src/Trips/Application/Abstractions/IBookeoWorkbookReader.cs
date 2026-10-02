using NorthernLink.Shared.Kernel;

namespace NorthernLink.Trips.Application.Abstractions;

/// <summary>
/// Reads an uploaded Bookeo export (.xls BIFF8 or .xlsx) into a plain cell grid — the sheet named
/// "Main", else the first sheet. Cells are <c>null</c>, <see cref="string"/>, <see cref="double"/>,
/// <see cref="bool"/> or <see cref="DateTime"/>, exactly as the workbook holds them. Fails with
/// <c>Trips.BookeoImport.UnsupportedFile</c> for anything that is not a readable workbook. The
/// grid is transient: nothing persists or logs it.
/// </summary>
public interface IBookeoWorkbookReader
{
    Result<IReadOnlyList<IReadOnlyList<object?>>> Read(Stream workbook);
}
