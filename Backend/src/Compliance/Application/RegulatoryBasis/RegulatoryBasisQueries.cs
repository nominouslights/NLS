using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Domain.RegulatoryBasis;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.RegulatoryBasis;

/// <summary>The catalogue entries for a set of keys — what a screen calls to explain a block or a record.</summary>
public sealed record GetRegulatoryBasisQuery(IReadOnlyList<string> Keys) : IQuery<IReadOnlyList<RegulatoryBasisResponse>>;

/// <summary>The whole catalogue, optionally one instrument's.</summary>
public sealed record ListRegulatoryBasisQuery(RegulatoryInstrument? Instrument) : IQuery<IReadOnlyList<RegulatoryBasisResponse>>;

/// <summary>
/// Handles <see cref="GetRegulatoryBasisQuery"/> — NL-SP-02 s. 11: every record and every refusal
/// names the rule it exists for, and this is how the rule's text is fetched. A key the catalogue
/// lacks is a NotFound, not a silently shorter list.
/// </summary>
public sealed class GetRegulatoryBasisQueryHandler(IRegulatoryBasisRepository repository)
    : IQueryHandler<GetRegulatoryBasisQuery, IReadOnlyList<RegulatoryBasisResponse>>
{
    public async Task<Result<IReadOnlyList<RegulatoryBasisResponse>>> Handle(GetRegulatoryBasisQuery query, CancellationToken cancellationToken)
    {
        var keys = query.Keys.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (keys.Count == 0)
        {
            return Result.Failure<IReadOnlyList<RegulatoryBasisResponse>>(RegulatoryBasisErrors.KeysRequired);
        }

        var rows = await repository.GetByKeysAsync(keys, cancellationToken);
        if (rows.Count != keys.Count)
        {
            return Result.Failure<IReadOnlyList<RegulatoryBasisResponse>>(RegulatoryBasisErrors.NotFound);
        }

        var byKey = rows.ToDictionary(r => r.Key, StringComparer.Ordinal);
        IReadOnlyList<RegulatoryBasisResponse> response = keys.Select(k => RegulatoryBasisResponse.From(byKey[k])).ToList();
        return Result.Success(response);
    }
}

/// <summary>Handles <see cref="ListRegulatoryBasisQuery"/> — NL-SP-02 s. 11.</summary>
public sealed class ListRegulatoryBasisQueryHandler(IRegulatoryBasisRepository repository)
    : IQueryHandler<ListRegulatoryBasisQuery, IReadOnlyList<RegulatoryBasisResponse>>
{
    public async Task<Result<IReadOnlyList<RegulatoryBasisResponse>>> Handle(ListRegulatoryBasisQuery query, CancellationToken cancellationToken)
    {
        var rows = await repository.ListAsync(query.Instrument, cancellationToken);
        IReadOnlyList<RegulatoryBasisResponse> response = rows.Select(RegulatoryBasisResponse.From).ToList();
        return Result.Success(response);
    }
}
