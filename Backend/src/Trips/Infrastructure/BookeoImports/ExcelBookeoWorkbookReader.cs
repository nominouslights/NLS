using System.Text;
using ExcelDataReader;
using NorthernLink.Shared.Kernel;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.BookeoImports;

namespace NorthernLink.Trips.Infrastructure.BookeoImports;

/// <summary>
/// <see cref="IBookeoWorkbookReader"/> over ExcelDataReader (MIT), which reads both the legacy
/// Excel 97 BIFF8 <c>.xls</c> Bookeo exports and <c>.xlsx</c>. BIFF8 strings are code-page
/// encoded, so <see cref="CodePagesEncodingProvider"/> is registered once, process-wide, before the
/// first read. Reads the sheet named "Main" (Bookeo's), falling back to the first sheet.
/// </summary>
internal sealed class ExcelBookeoWorkbookReader : IBookeoWorkbookReader
{
    private const string BookeoSheetName = "Main";

    static ExcelBookeoWorkbookReader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Result<IReadOnlyList<IReadOnlyList<object?>>> Read(Stream workbook)
    {
        try
        {
            using var reader = ExcelReaderFactory.CreateReader(workbook);

            List<IReadOnlyList<object?>>? first = null;
            do
            {
                var grid = new List<IReadOnlyList<object?>>();
                while (reader.Read())
                {
                    var cells = new object?[reader.FieldCount];
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        cells[i] = reader.GetValue(i);
                    }

                    grid.Add(cells);
                }

                if (string.Equals(reader.Name, BookeoSheetName, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Success<IReadOnlyList<IReadOnlyList<object?>>>(grid);
                }

                first ??= grid;
            }
            while (reader.NextResult());

            return Result.Success<IReadOnlyList<IReadOnlyList<object?>>>(first ?? []);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            // ExcelDataReader signals "not a workbook" with a family of exception types
            // (HeaderException, InvalidDataException, …); to the caller they all mean one thing.
            return Result.Failure<IReadOnlyList<IReadOnlyList<object?>>>(BookeoImportErrors.UnsupportedFile);
        }
    }
}
