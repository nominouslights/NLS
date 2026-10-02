using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Application.Abstractions;
using NorthernLink.Trips.Domain.BookeoImports;

namespace NorthernLink.Trips.Application.BookeoImports.Mappings;

public sealed class GetBookeoProductMappingsQueryHandler(IBookeoImportRepository repository)
    : IQueryHandler<GetBookeoProductMappingsQuery, IReadOnlyList<BookeoProductMappingResponse>>
{
    public async Task<Result<IReadOnlyList<BookeoProductMappingResponse>>> Handle(
        GetBookeoProductMappingsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await BookeoMappingResponses.ProductsAsync(repository, cancellationToken));
}

public sealed class UpsertBookeoProductMappingsCommandHandler(IBookeoImportRepository repository)
    : ICommandHandler<UpsertBookeoProductMappingsCommand, IReadOnlyList<BookeoProductMappingResponse>>
{
    public async Task<Result<IReadOnlyList<BookeoProductMappingResponse>>> Handle(
        UpsertBookeoProductMappingsCommand command, CancellationToken cancellationToken)
    {
        var inputs = command.Mappings ?? [];
        var keys = inputs
            .Select(m => (Code: BookeoText.Clean(m.ProductCode), Destination: BookeoText.Clean(m.Destination)?.ToUpperInvariant()))
            .ToList();
        if (keys.Distinct().Count() != keys.Count)
        {
            return Result.Failure<IReadOnlyList<BookeoProductMappingResponse>>(BookeoImportErrors.DuplicateMapping);
        }

        var routeIds = inputs.Select(m => m.RouteId).Where(id => id != Guid.Empty).Distinct().ToList();
        var routes = (await repository.GetRoutesAsync(routeIds, cancellationToken)).Select(r => r.Id).ToHashSet();
        var existing = await repository.GetProductMappingsAsync(cancellationToken);

        foreach (var input in inputs)
        {
            if (input.RouteId != Guid.Empty && !routes.Contains(input.RouteId))
            {
                return Result.Failure<IReadOnlyList<BookeoProductMappingResponse>>(BookeoImportErrors.RouteNotFound);
            }

            var current = existing.FirstOrDefault(m => m.HasKey(input.ProductCode, input.Destination));
            if (current is not null)
            {
                var updated = current.Update(
                    input.ProductCode, input.ProductName, input.Destination, input.RouteId, input.Direction, input.ResidentStopRole);
                if (updated.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<BookeoProductMappingResponse>>(updated.Error);
                }

                continue;
            }

            var created = BookeoProductMapping.Create(
                command.TenantId, input.ProductCode, input.ProductName, input.Destination, input.RouteId, input.Direction, input.ResidentStopRole);
            if (created.IsFailure)
            {
                return Result.Failure<IReadOnlyList<BookeoProductMappingResponse>>(created.Error);
            }

            repository.AddProductMapping(created.Value);
        }

        if (await repository.SaveAsync(cancellationToken) != BookeoSaveOutcome.Saved)
        {
            return Result.Failure<IReadOnlyList<BookeoProductMappingResponse>>(BookeoImportErrors.MappingConflict);
        }

        return Result.Success(await BookeoMappingResponses.ProductsAsync(repository, cancellationToken));
    }
}

public sealed class DeleteBookeoProductMappingCommandHandler(IBookeoImportRepository repository)
    : ICommandHandler<DeleteBookeoProductMappingCommand>
{
    public async Task<Result> Handle(DeleteBookeoProductMappingCommand command, CancellationToken cancellationToken)
    {
        var mapping = (await repository.GetProductMappingsAsync(cancellationToken))
            .FirstOrDefault(m => m.Id == command.MappingId);
        if (mapping is null)
        {
            return Result.Failure(BookeoImportErrors.MappingNotFound);
        }

        repository.RemoveProductMapping(mapping);
        await repository.SaveAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetBookeoUnitMappingsQueryHandler(IBookeoImportRepository repository)
    : IQueryHandler<GetBookeoUnitMappingsQuery, IReadOnlyList<BookeoUnitMappingResponse>>
{
    public async Task<Result<IReadOnlyList<BookeoUnitMappingResponse>>> Handle(
        GetBookeoUnitMappingsQuery query, CancellationToken cancellationToken) =>
        Result.Success(await BookeoMappingResponses.UnitsAsync(repository, cancellationToken));
}

public sealed class UpsertBookeoUnitMappingsCommandHandler(IBookeoImportRepository repository)
    : ICommandHandler<UpsertBookeoUnitMappingsCommand, IReadOnlyList<BookeoUnitMappingResponse>>
{
    public async Task<Result<IReadOnlyList<BookeoUnitMappingResponse>>> Handle(
        UpsertBookeoUnitMappingsCommand command, CancellationToken cancellationToken)
    {
        var inputs = command.Mappings ?? [];
        var keys = inputs.Select(m => BookeoText.NormalizeUnit(m.UnitText)).ToList();
        if (keys.Distinct().Count() != keys.Count)
        {
            return Result.Failure<IReadOnlyList<BookeoUnitMappingResponse>>(BookeoImportErrors.DuplicateMapping);
        }

        var vehicles = (await repository.GetVehiclesAsync(cancellationToken)).Select(v => v.VehicleId).ToHashSet();
        var existing = await repository.GetUnitMappingsAsync(cancellationToken);

        foreach (var input in inputs)
        {
            if (!vehicles.Contains(input.VehicleId))
            {
                return Result.Failure<IReadOnlyList<BookeoUnitMappingResponse>>(BookeoImportErrors.VehicleNotFound);
            }

            var normalized = BookeoText.NormalizeUnit(input.UnitText);
            var current = existing.FirstOrDefault(m => m.UnitText == normalized);
            var result = current is not null
                ? current.Update(input.UnitText, input.VehicleId)
                : AddNew(command.TenantId, input);
            if (result.IsFailure)
            {
                return Result.Failure<IReadOnlyList<BookeoUnitMappingResponse>>(result.Error);
            }
        }

        if (await repository.SaveAsync(cancellationToken) != BookeoSaveOutcome.Saved)
        {
            return Result.Failure<IReadOnlyList<BookeoUnitMappingResponse>>(BookeoImportErrors.MappingConflict);
        }

        return Result.Success(await BookeoMappingResponses.UnitsAsync(repository, cancellationToken));
    }

    private Result AddNew(Guid tenantId, BookeoUnitMappingInput input)
    {
        var created = BookeoUnitMapping.Create(tenantId, input.UnitText, input.VehicleId);
        if (created.IsFailure)
        {
            return created;
        }

        repository.AddUnitMapping(created.Value);
        return Result.Success();
    }
}

public sealed class DeleteBookeoUnitMappingCommandHandler(IBookeoImportRepository repository)
    : ICommandHandler<DeleteBookeoUnitMappingCommand>
{
    public async Task<Result> Handle(DeleteBookeoUnitMappingCommand command, CancellationToken cancellationToken)
    {
        var mapping = (await repository.GetUnitMappingsAsync(cancellationToken))
            .FirstOrDefault(m => m.Id == command.MappingId);
        if (mapping is null)
        {
            return Result.Failure(BookeoImportErrors.MappingNotFound);
        }

        repository.RemoveUnitMapping(mapping);
        await repository.SaveAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class GetBookeoImportBatchesQueryHandler(IBookeoImportRepository repository)
    : IQueryHandler<GetBookeoImportBatchesQuery, IReadOnlyList<BookeoImportBatchResponse>>
{
    public const int DefaultTake = 20;
    public const int MaxTake = 100;

    public async Task<Result<IReadOnlyList<BookeoImportBatchResponse>>> Handle(
        GetBookeoImportBatchesQuery query, CancellationToken cancellationToken)
    {
        var take = query.Take <= 0 ? DefaultTake : Math.Min(query.Take, MaxTake);
        var batches = await repository.GetRecentBatchesAsync(take, cancellationToken);
        IReadOnlyList<BookeoImportBatchResponse> response = batches
            .Select(b => new BookeoImportBatchResponse(
                b.Id,
                b.FileName,
                b.UploadedBy,
                b.UploadedAtUtc,
                b.CommittedAtUtc,
                b.CommittedBy,
                BookeoImportJson.DeserializeSummary(b.SummaryJson)))
            .ToList();
        return Result.Success(response);
    }
}

/// <summary>Builds the mapping list responses (shared by the GET and the PUT that returns the list).</summary>
internal static class BookeoMappingResponses
{
    public static async Task<IReadOnlyList<BookeoProductMappingResponse>> ProductsAsync(
        IBookeoImportRepository repository, CancellationToken cancellationToken)
    {
        var mappings = await repository.GetProductMappingsAsync(cancellationToken);
        var routes = (await repository.GetRoutesAsync(mappings.Select(m => m.RouteId).Distinct().ToList(), cancellationToken))
            .ToDictionary(r => r.Id, r => r.Name);

        return mappings
            .OrderBy(m => m.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Destination, StringComparer.OrdinalIgnoreCase)
            .Select(m => new BookeoProductMappingResponse(
                m.Id,
                m.ProductCode,
                m.ProductName,
                m.Destination,
                m.RouteId,
                routes.GetValueOrDefault(m.RouteId) ?? "(route removed)",
                m.Direction?.ToString(),
                m.ResidentStopRole.ToString()))
            .ToList();
    }

    public static async Task<IReadOnlyList<BookeoUnitMappingResponse>> UnitsAsync(
        IBookeoImportRepository repository, CancellationToken cancellationToken)
    {
        var mappings = await repository.GetUnitMappingsAsync(cancellationToken);
        var vehicles = (await repository.GetVehiclesAsync(cancellationToken)).ToDictionary(v => v.VehicleId, v => v.UnitNumber);

        return mappings
            .OrderBy(m => m.UnitText, StringComparer.Ordinal)
            .Select(m => new BookeoUnitMappingResponse(m.Id, m.UnitText, m.VehicleId, vehicles.GetValueOrDefault(m.VehicleId)))
            .ToList();
    }
}
