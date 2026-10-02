using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Application.BookeoImports.Parsing;
using NorthernLink.Trips.Domain.BookeoImports;

namespace NorthernLink.Trips.Application.BookeoImports.Preview;

public sealed class PreviewBookeoImportCommandHandler(
    IBookeoWorkbookReader workbookReader,
    BookeoImportPlanLoader planLoader,
    IBookeoImportRepository repository,
    TimeProvider clock)
    : ICommandHandler<PreviewBookeoImportCommand, BookeoImportPreview>
{
    private static readonly string[] AcceptedExtensions = [".xls", ".xlsx"];

    public async Task<Result<BookeoImportPreview>> Handle(
        PreviewBookeoImportCommand command, CancellationToken cancellationToken)
    {
        if (command.Content is not { Length: > 0 })
        {
            return Result.Failure<BookeoImportPreview>(BookeoImportErrors.FileRequired);
        }

        if (command.Content.Length > BookeoImportErrors.MaxFileBytes)
        {
            return Result.Failure<BookeoImportPreview>(BookeoImportErrors.FileTooLarge);
        }

        var extension = Path.GetExtension(command.FileName ?? string.Empty).ToLowerInvariant();
        if (!AcceptedExtensions.Contains(extension))
        {
            return Result.Failure<BookeoImportPreview>(BookeoImportErrors.UnsupportedFile);
        }

        using var stream = new MemoryStream(command.Content, writable: false);
        var grid = workbookReader.Read(stream);
        if (grid.IsFailure)
        {
            return Result.Failure<BookeoImportPreview>(grid.Error);
        }

        var parsed = BookeoReportParser.Parse(grid.Value);
        if (parsed.IsFailure)
        {
            return Result.Failure<BookeoImportPreview>(parsed.Error);
        }

        if (parsed.Value.Count > BookeoImportErrors.MaxRows)
        {
            return Result.Failure<BookeoImportPreview>(BookeoImportErrors.TooManyRows(parsed.Value.Count));
        }

        var plan = await planLoader.PlanAsync(parsed.Value, cancellationToken);

        var batch = BookeoImportBatch.Create(
            command.TenantId,
            Path.GetFileName(command.FileName ?? string.Empty),
            command.UploadedBy,
            clock.GetUtcNow(),
            BookeoImportJson.SerializeRows(parsed.Value),
            plan.Hash,
            BookeoImportJson.SerializeSummary(plan.Summary));

        repository.AddBatch(batch);
        await repository.SaveAsync(cancellationToken);

        return Result.Success(plan.ToPreview(batch.Id, batch.FileName, batch.UploadedAtUtc));
    }
}
