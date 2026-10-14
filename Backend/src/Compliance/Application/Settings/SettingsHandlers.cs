using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.RegulatoryBasis;
using NorthernLink.Compliance.Domain.Numbering;
using NorthernLink.Compliance.Domain.RuleSets;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;

namespace NorthernLink.Compliance.Application.Settings;

/// <summary>Handles <see cref="GetComplianceSettingsQuery"/> — HTA s. 318.8(2) (the one place records are kept).</summary>
public sealed class GetComplianceSettingsQueryHandler(ITenantComplianceSettingsRepository settings)
    : IQueryHandler<GetComplianceSettingsQuery, ComplianceSettingsResponse>
{
    public async Task<Result<ComplianceSettingsResponse>> Handle(GetComplianceSettingsQuery query, CancellationToken cancellationToken)
    {
        var row = await settings.GetAsync(cancellationToken);
        return row is null
            ? Result.Failure<ComplianceSettingsResponse>(ComplianceSettingsErrors.NotFound)
            : Result.Success(ComplianceSettingsResponse.From(row));
    }
}

/// <summary>
/// Handles <see cref="UpdateComplianceSettingsCommand"/> — HTA s. 314 / M.R. 132/2024 s. 9 (the
/// carrier's name and NSC number identify every document) and s. 318.8(2) (principal place of
/// business). The save goes through the record repository's unit of work.
/// <para>
/// Once the carrier authority holds an NSC number it owns it: a PUT whose NSC differs is refused
/// (409 <c>Compliance.Settings.NscNumberManagedByAuthority</c>) — echo the number GET returned.
/// Until the authority records one, the field is still edited here.
/// </para>
/// </summary>
public sealed class UpdateComplianceSettingsCommandHandler(
    ITenantComplianceSettingsRepository settings,
    IComplianceRecordRepository unitOfWork,
    ICarrierAuthorityRepository authorities)
    : ICommandHandler<UpdateComplianceSettingsCommand>
{
    public async Task<Result> Handle(UpdateComplianceSettingsCommand command, CancellationToken cancellationToken)
    {
        if (await authorities.GetAsync(cancellationToken) is { NscNumber: { } authorityNsc }
            && TenantComplianceSettings.NormalizedNscNumber(command.Details) != authorityNsc)
        {
            return Result.Failure(ComplianceSettingsErrors.NscNumberManagedByAuthority);
        }

        var row = await settings.GetAsync(cancellationToken);
        if (row is null)
        {
            var created = TenantComplianceSettings.Create(command.TenantId, command.Details, command.ActorId);
            if (created.IsFailure)
            {
                return Result.Failure(created.Error);
            }

            settings.Add(created.Value);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        var updated = row.Update(command.Details, command.ActorId);
        if (updated.IsFailure)
        {
            return updated;
        }

        if (row.DomainEvents.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

/// <summary>
/// Handles <see cref="SeedComplianceDefaultsCommand"/> — the Budgeting starter-set precedent:
/// goes through the aggregates so every seeded row carries its audit trail and lands under the
/// caller's tenant. Reference rows (rule sets, basis catalogue) are written by their own
/// repositories under the system session; tenant rows share one SaveChanges.
/// NL-SP-02 ss. 11 and 13; HTA s. 318.8(2).
/// </summary>
public sealed class SeedComplianceDefaultsCommandHandler(
    ITenantComplianceSettingsRepository settings,
    IDocumentSeriesRepository series,
    IComplianceRecordRepository unitOfWork,
    IRuleSetRepository ruleSets,
    ITenantRuleSetApplicationRepository applications,
    IRegulatoryBasisRepository basis)
    : ICommandHandler<SeedComplianceDefaultsCommand, SeedDefaultsResult>
{
    public async Task<Result<SeedDefaultsResult>> Handle(SeedComplianceDefaultsCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Reference data first (system session, own saves): the catalogue and the rule set.
        var basisInserted = await basis.UpsertAsync(RegulatoryBasisSeed.All, cancellationToken);

        var publishedNow = false;
        var current = await ruleSets.FindAsync(CaMbRuleSets.Current.Jurisdiction, CaMbRuleSets.Current.Version, cancellationToken);
        if (current is null)
        {
            current = RuleSet.Publish(CaMbRuleSets.Current, now);
            await ruleSets.AddAsync(current, cancellationToken);
            publishedNow = true;
        }

        // 2. Tenant rows, one save.
        var settingsCreated = false;
        if (await settings.GetAsync(cancellationToken) is null)
        {
            settings.Add(TenantComplianceSettings.CreateDefault(command.TenantId, command.ActorId));
            settingsCreated = true;
        }

        var existingSeries = (await series.ListAsync(now.Year, cancellationToken)).Select(s => s.Series).ToHashSet();
        var seriesCreated = 0;
        foreach (var code in Enum.GetValues<DocumentSeriesCode>())
        {
            if (existingSeries.Contains(code))
            {
                continue;
            }

            series.Add(DocumentSeries.Open(command.TenantId, code, now.Year));
            seriesCreated++;
        }

        var appliedNow = false;
        if (await applications.LatestAsOfAsync(now, cancellationToken) is null)
        {
            var application = TenantRuleSetApplication.Apply(
                command.TenantId, current, "Initial adoption of the CA-MB rule set (seed-defaults).", command.ActorId, now);
            if (application.IsFailure)
            {
                return Result.Failure<SeedDefaultsResult>(application.Error);
            }

            applications.Add(application.Value);
            appliedNow = true;
        }

        if (settingsCreated || seriesCreated > 0 || appliedNow)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new SeedDefaultsResult(settingsCreated, publishedNow, appliedNow, seriesCreated, basisInserted));
    }
}
