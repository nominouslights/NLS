using Microsoft.EntityFrameworkCore;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.Records;
using NorthernLink.Compliance.Infrastructure.Persistence;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Tenancy;
using Xunit;

namespace NorthernLink.Compliance.Tests;

/// <summary>
/// <c>ComplianceRecordRepository.TrySaveChangesAsync</c> recognises its expected races by the
/// Postgres constraint name on the unique violation. A renamed index would silently turn a
/// retryable race back into a 500 (or a parked intake event), so the names it matches are pinned
/// against the real EF model — building the model needs no database connection.
/// </summary>
public class ComplianceUniqueIndexNameTests
{
    private static ComplianceDbContext Context() =>
        new(new DbContextOptionsBuilder<ComplianceDbContext>().UseNpgsql("Host=unused").Options, new NoTenant());

    private static string? UniqueIndexName<TEntity>(ComplianceDbContext context, params string[] properties)
    {
        var index = context.Model.FindEntityType(typeof(TEntity))!.GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(properties));
        return index.GetDatabaseName();
    }

    [Fact]
    public void The_constraint_names_the_repository_matches_are_the_models_index_names()
    {
        using var context = Context();

        Assert.Equal(
            ComplianceRecordConfiguration.SourceIndex,
            UniqueIndexName<ComplianceRecord>(context, nameof(ComplianceRecord.TenantId), nameof(ComplianceRecord.SourceModule), nameof(ComplianceRecord.SourceRecordId)));
        Assert.Equal(
            DocumentSeriesConfiguration.SeriesYearIndex,
            UniqueIndexName<DocumentSeries>(context, nameof(DocumentSeries.TenantId), nameof(DocumentSeries.Series), nameof(DocumentSeries.Year)));
        Assert.Equal(
            TenantComplianceSettingsConfiguration.TenantIndex,
            UniqueIndexName<TenantComplianceSettings>(context, nameof(TenantComplianceSettings.TenantId)));
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;

        public TenantType? TenantType => null;
    }
}
