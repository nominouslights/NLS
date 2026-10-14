using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Compliance.Application;
using NorthernLink.Compliance.Application.Abstractions;
using NorthernLink.Compliance.Application.Integration;
using NorthernLink.Compliance.Application.Printing;
using NorthernLink.Compliance.Application.Records;
using NorthernLink.Compliance.Application.Records.Dispose;
using NorthernLink.Compliance.Application.Records.Holds;
using NorthernLink.Compliance.Application.Records.LinkSupportingDocument;
using NorthernLink.Compliance.Application.Records.Print;
using NorthernLink.Compliance.Application.Records.Queries;
using NorthernLink.Compliance.Application.Records.QueueForDisposalReview;
using NorthernLink.Compliance.Application.Records.Register;
using NorthernLink.Compliance.Application.Records.SetAnchorDate;
using NorthernLink.Compliance.Application.Records.Supersede;
using NorthernLink.Compliance.Application.RegulatoryBasis;
using NorthernLink.Compliance.Application.RuleSets;
using NorthernLink.Compliance.Application.Settings;
using NorthernLink.Compliance.Application.Authority;
using NorthernLink.Compliance.Application.Insurance;
using NorthernLink.Compliance.Application.Registrations;
using NorthernLink.Compliance.Application.Vehicles;
using NorthernLink.Compliance.Domain.Signatures;
using NorthernLink.Compliance.Infrastructure.Persistence;
using NorthernLink.Compliance.Infrastructure.Persistence.Projections;
using NorthernLink.Compliance.Infrastructure.Printing;
using NorthernLink.Compliance.Infrastructure.Retention;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Compliance;
using NorthernLink.Shared.IntegrationEvents.Drivers;
using NorthernLink.Shared.IntegrationEvents.Fleet;
using NorthernLink.Shared.IntegrationEvents.Identity;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;
using QuestPDF.Infrastructure;

namespace NorthernLink.Compliance.Infrastructure;

/// <summary>
/// DI entry point for the Compliance domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "compliance"), persistence services, the
/// retention worker, the print service, and every CQRS handler explicitly (the reflection-based
/// Sender resolves handlers from DI; no assembly scanning).
/// </summary>
public static class ComplianceServiceCollectionExtensions
{
    public const string SchemaName = "compliance";

    /// <summary>
    /// The producer schemas whose outboxes carry <see cref="ComplianceRecordProducedIntegrationEvent"/>.
    /// Only modules with a schema today — Incidents and Grocery are stubs without an
    /// <c>outbox_messages</c> table, and polling a missing table would fail every tick; add them
    /// here the day they get a DbContext. <c>compliance</c> itself is one: this module registers
    /// its own R20/R21 documents (SFC, insurance, registration, later the plan) through the same
    /// event, written to its own outbox by <see cref="ComplianceIntegrationEventMapper"/>.
    /// </summary>
    public static readonly string[] RecordProducerSchemas = ["compliance", "drivers", "fleet", "trips"];

    public static IServiceCollection AddCompliance(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        //    The connection-string lookup stays inside the factory lambda — registration
        //    itself must need no environment (HandlerRegistrationRulesTests invokes it bare).
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<ComplianceDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains compliance.outbox_messages.
        services.AddScoped<ComplianceIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<ComplianceDbContext>>();
        services.AddScoped<IComplianceRecordRepository, ComplianceRecordRepository>();
        services.AddScoped<IComplianceRecordReadService, ComplianceRecordReadService>();
        services.AddScoped<IDocumentSeriesRepository, DocumentSeriesRepository>();
        services.AddScoped<ITenantComplianceSettingsRepository, TenantComplianceSettingsRepository>();
        services.AddScoped<IDestructionLogRepository, DestructionLogRepository>();
        services.AddScoped<IRuleSetRepository, RuleSetRepository>();
        services.AddScoped<ITenantRuleSetApplicationRepository, TenantRuleSetApplicationRepository>();
        services.AddScoped<IRegulatoryBasisRepository, RegulatoryBasisRepository>();
        services.AddScoped<IRetentionSweepSource, RetentionSweepSource>();
        services.AddScoped<RecordRegistrar>();
        services.AddScoped<ActiveRuleSetResolver>();
        services.AddScoped<ICarrierAuthorityRepository, CarrierAuthorityRepository>();
        services.AddScoped<IInsurancePolicyRepository, InsurancePolicyRepository>();
        services.AddScoped<IVehicleRegistrationRepository, VehicleRegistrationRepository>();
        services.AddScoped<IVehicleLookupRepository, VehicleLookupRepository>();
        services.AddScoped<IDriverLookupRepository, DriverLookupRepository>();
        services.AddScoped<IUserLookupRepository, UserLookupRepository>();

        // Where a registered-record answer for one of this module's own documents is stamped —
        // one target per source-id prefix (see ComplianceRecordRegisteredIntegrationEventHandler).
        services.AddScoped<IRecordStampTarget, SfcRecordStampTarget>();
        services.AddScoped<IRecordStampTarget, InsurancePolicyRecordStampTarget>();
        services.AddScoped<IRecordStampTarget, VehicleRegistrationRecordStampTarget>();

        // Record prints (QuestPDF). The Community license must be set once, process-wide, before
        // any document is generated — a static assignment here guarantees it runs before the
        // first render. Singleton: the renderer holds no mutable state.
        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IRecordPrintService, QuestRecordPrintService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<RegisterRecordCommand, RegisterRecordResult>, RegisterRecordCommandHandler>();
        services.AddScoped<ICommandHandler<SupersedeRecordCommand, RegisterRecordResult>, SupersedeRecordCommandHandler>();
        services.AddScoped<ICommandHandler<LinkSupportingDocumentCommand>, LinkSupportingDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<SetAnchorDateCommand>, SetAnchorDateCommandHandler>();
        services.AddScoped<ICommandHandler<PlaceHoldCommand, Guid>, PlaceHoldCommandHandler>();
        services.AddScoped<ICommandHandler<ReleaseHoldCommand>, ReleaseHoldCommandHandler>();
        services.AddScoped<ICommandHandler<DisposeRecordsCommand, DisposalResult>, DisposeRecordsCommandHandler>();
        services.AddScoped<ICommandHandler<QueueForDisposalReviewCommand, int>, QueueForDisposalReviewCommandHandler>();
        services.AddScoped<IQueryHandler<GetRecordQuery, ComplianceRecordResponse>, GetRecordQueryHandler>();
        services.AddScoped<IQueryHandler<ListRecordsQuery, IReadOnlyList<ComplianceRecordResponse>>, ListRecordsQueryHandler>();
        services.AddScoped<IQueryHandler<ListRecordsDueForDisposalQuery, IReadOnlyList<ComplianceRecordResponse>>, ListRecordsDueForDisposalQueryHandler>();
        services.AddScoped<IQueryHandler<GetRetentionStatusQuery, RetentionStatusResponse>, GetRetentionStatusQueryHandler>();
        services.AddScoped<IQueryHandler<ListDestructionLogQuery, IReadOnlyList<DestructionLogEntryResponse>>, ListDestructionLogQueryHandler>();
        services.AddScoped<IQueryHandler<GetRecordTypeCatalogueQuery, IReadOnlyList<RecordTypeResponse>>, GetRecordTypeCatalogueQueryHandler>();
        services.AddScoped<IQueryHandler<GetRecordPrintQuery, RecordPrintResult>, GetRecordPrintQueryHandler>();
        services.AddScoped<ICommandHandler<PublishRuleSetsCommand, RuleSetPublishResult>, PublishRuleSetsCommandHandler>();
        services.AddScoped<ICommandHandler<ApplyTenantRuleSetCommand, Guid>, ApplyTenantRuleSetCommandHandler>();
        services.AddScoped<IQueryHandler<GetActiveRulesQuery, ActiveRulesResponse>, GetActiveRulesQueryHandler>();
        services.AddScoped<IQueryHandler<GetRuleValueQuery, RuleValueResponse>, GetRuleValueQueryHandler>();
        services.AddScoped<IQueryHandler<ListRuleSetsQuery, IReadOnlyList<RuleSetSummaryResponse>>, ListRuleSetsQueryHandler>();
        services.AddScoped<IQueryHandler<GetRegulatoryBasisQuery, IReadOnlyList<RegulatoryBasisResponse>>, GetRegulatoryBasisQueryHandler>();
        services.AddScoped<IQueryHandler<ListRegulatoryBasisQuery, IReadOnlyList<RegulatoryBasisResponse>>, ListRegulatoryBasisQueryHandler>();
        services.AddScoped<IQueryHandler<GetComplianceSettingsQuery, ComplianceSettingsResponse>, GetComplianceSettingsQueryHandler>();
        services.AddScoped<ICommandHandler<UpdateComplianceSettingsCommand>, UpdateComplianceSettingsCommandHandler>();
        services.AddScoped<ICommandHandler<SeedComplianceDefaultsCommand, SeedDefaultsResult>, SeedComplianceDefaultsCommandHandler>();

        // Carrier authority (NL-SP-02 ss. 1–2), vehicles, insurance and registration (s. 12.1, R20).
        services.AddScoped<IQueryHandler<GetCarrierAuthorityQuery, CarrierAuthorityResponse>, GetCarrierAuthorityQueryHandler>();
        services.AddScoped<ICommandHandler<RecordSfcCommand>, RecordSfcCommandHandler>();
        services.AddScoped<ICommandHandler<RecordNscNumberCommand>, RecordNscNumberCommandHandler>();
        services.AddScoped<ICommandHandler<SetSafetyRatingCommand>, SetSafetyRatingCommandHandler>();
        services.AddScoped<ICommandHandler<DeclareIntraProvincialCommand>, DeclareIntraProvincialCommandHandler>();
        services.AddScoped<ICommandHandler<SetOperatingRoutesCommand>, SetOperatingRoutesCommandHandler>();
        services.AddScoped<ICommandHandler<AttachSfcCertificateCommand>, AttachSfcCertificateCommandHandler>();
        services.AddScoped<IQueryHandler<GetSfcAttachmentQuery, AttachmentRef>, GetSfcAttachmentQueryHandler>();
        services.AddScoped<ICommandHandler<SetVehicleRegulatedCommand>, SetVehicleRegulatedCommandHandler>();
        services.AddScoped<IQueryHandler<ListComplianceVehiclesQuery, IReadOnlyList<ComplianceVehicleResponse>>, ListComplianceVehiclesQueryHandler>();
        services.AddScoped<ICommandHandler<RecordInsurancePolicyCommand, Guid>, RecordInsurancePolicyCommandHandler>();
        services.AddScoped<ICommandHandler<RenewInsurancePolicyCommand, Guid>, RenewInsurancePolicyCommandHandler>();
        services.AddScoped<ICommandHandler<AttachInsuranceDocumentCommand>, AttachInsuranceDocumentCommandHandler>();
        services.AddScoped<IQueryHandler<GetInsurancePolicyQuery, InsurancePolicyResponse>, GetInsurancePolicyQueryHandler>();
        services.AddScoped<IQueryHandler<ListInsurancePoliciesQuery, IReadOnlyList<InsurancePolicyResponse>>, ListInsurancePoliciesQueryHandler>();
        services.AddScoped<IQueryHandler<GetInsuranceAttachmentQuery, AttachmentRef>, GetInsuranceAttachmentQueryHandler>();
        services.AddScoped<ICommandHandler<RecordVehicleRegistrationCommand, Guid>, RecordVehicleRegistrationCommandHandler>();
        services.AddScoped<ICommandHandler<RenewVehicleRegistrationCommand, Guid>, RenewVehicleRegistrationCommandHandler>();
        services.AddScoped<ICommandHandler<AttachRegistrationDocumentCommand>, AttachRegistrationDocumentCommandHandler>();
        services.AddScoped<IQueryHandler<GetVehicleRegistrationQuery, VehicleRegistrationResponse>, GetVehicleRegistrationQueryHandler>();
        services.AddScoped<IQueryHandler<ListVehicleRegistrationsQuery, IReadOnlyList<VehicleRegistrationResponse>>, ListVehicleRegistrationsQueryHandler>();
        services.AddScoped<IQueryHandler<GetRegistrationAttachmentQuery, AttachmentRef>, GetRegistrationAttachmentQueryHandler>();

        // 4. Integration event consumer — the record registry's intake. The produced event lives
        //    in IntegrationEvents/Compliance (routing key compliance.compliance-record-produced),
        //    but it is written to the PRODUCER's outbox, so the poller is pointed at those schemas
        //    explicitly (OnProducedBy) rather than at the key's own "compliance" segment.
        services.AddOutboxPollingConsumer<ComplianceDbContext>(SchemaName, subscriptions => subscriptions
            .OnProducedBy<ComplianceRecordProducedIntegrationEvent, ComplianceRecordProducedIntegrationEventHandler>(RecordProducerSchemas)
            // Fan-out, not status-tracked: every outbox row has ONE processing_status, so a second
            // status consumer of a key steals rows from the first. Trips already consumes the
            // vehicle and driver streams and Budgeting the user stream; the registered answer is
            // fan-out so a future producer module can consume it beside this self-consumer.
            .OnFanOut<ComplianceRecordRegisteredIntegrationEvent, ComplianceRecordRegisteredIntegrationEventHandler>()
            .OnFanOut<VehicleChangedIntegrationEvent, VehicleChangedIntegrationEventHandler>()
            .OnFanOut<DriverChangedIntegrationEvent, DriverChangedIntegrationEventHandler>()
            .OnFanOut<UserChangedIntegrationEvent, UserChangedIntegrationEventHandler>());

        // 5. Read-side projections — one worker polls compliance.event_journal and upserts
        //    the read-model rows for the aggregates the batch touched.
        services.AddProjections<ComplianceDbContext>(SchemaName, registry => registry
            .Project(new ComplianceRecordProjection()));

        // 6. The nightly retention sweep.
        var retentionOptions = configuration.GetSection(RetentionOptions.SectionName).Get<RetentionOptions>()
            ?? new RetentionOptions();
        services.AddSingleton(retentionOptions);
        services.AddHostedService<RetentionWorker>();

        return services;
    }
}
