using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Drivers.Application.Abstractions;
using NorthernLink.Drivers.Application.Clearances;
using NorthernLink.Drivers.Application.Clearances.GetForDriver;
using NorthernLink.Drivers.Application.Clearances.Grant;
using NorthernLink.Drivers.Application.Clearances.Revoke;
using NorthernLink.Drivers.Application.Credentials;
using NorthernLink.Drivers.Application.Credentials.Add;
using NorthernLink.Drivers.Application.Credentials.GetForDriver;
using NorthernLink.Drivers.Application.Credentials.Remove;
using NorthernLink.Drivers.Application.Credentials.SetImage;
using NorthernLink.Drivers.Application.Drivers;
using NorthernLink.Drivers.Application.Drivers.AttestPriorRecords;
using NorthernLink.Drivers.Application.Drivers.ChangeStatus;
using NorthernLink.Drivers.Application.Drivers.GetByUser;
using NorthernLink.Drivers.Application.Drivers.GetDriverById;
using NorthernLink.Drivers.Application.Drivers.GetDrivers;
using NorthernLink.Drivers.Application.Drivers.LinkUser;
using NorthernLink.Drivers.Application.Drivers.Register;
using NorthernLink.Drivers.Application.Drivers.SelfAccess;
using NorthernLink.Drivers.Application.Drivers.UnlinkUser;
using NorthernLink.Drivers.Application.Drivers.Update;
using NorthernLink.Drivers.Application.Hos;
using NorthernLink.Drivers.Application.Hos.Audit;
using NorthernLink.Drivers.Application.Hos.Compliance;
using NorthernLink.Drivers.Application.Hos.DutyDays.Certify;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetOne;
using NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;
using NorthernLink.Drivers.Application.Hos.DutyDays.Transcribe;
using NorthernLink.Drivers.Application.Hos.DutyEvents.Amend;
using NorthernLink.Drivers.Application.Hos.DutyEvents.Record;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Get;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Report;
using NorthernLink.Drivers.Application.Hos.EldMalfunctions.Resolve;
using NorthernLink.Drivers.Application.Hos.Exceptions.Acknowledge;
using NorthernLink.Drivers.Application.Hos.Exceptions.ApproveDeferral;
using NorthernLink.Drivers.Application.Hos.Exceptions.Declare;
using NorthernLink.Drivers.Application.Hos.Exceptions.Get;
using NorthernLink.Drivers.Application.Hos.Exceptions.Reject;
using NorthernLink.Drivers.Application.Hos.Exceptions.Withdraw;
using NorthernLink.Drivers.Application.Hos.Findings.Get;
using NorthernLink.Drivers.Application.Hos.Findings.Open;
using NorthernLink.Drivers.Application.Hos.Findings.Update;
using NorthernLink.Drivers.Application.Hos.GetForDriver;
using NorthernLink.Drivers.Application.Hos.OutOfService.Get;
using NorthernLink.Drivers.Application.Hos.OutOfService.Lift;
using NorthernLink.Drivers.Application.Hos.OutOfService.Record;
using NorthernLink.Drivers.Application.Hos.Record;
using NorthernLink.Drivers.Application.Hos.Status;
using NorthernLink.Drivers.Infrastructure.Persistence;
using NorthernLink.Drivers.Infrastructure.Persistence.Projections;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Tenancy;

namespace NorthernLink.Drivers.Infrastructure;

/// <summary>
/// DI entry point for the Drivers domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "drivers"), persistence services,
/// and every CQRS handler explicitly
/// (the reflection-based Sender resolves handlers from DI; no assembly scanning).
/// </summary>
public static class DriversServiceCollectionExtensions
{
    public const string SchemaName = "drivers";

    public static IServiceCollection AddDrivers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<DriversDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be
        //    a single DI registration. The outbox dispatcher drains drivers.outbox_messages.
        services.AddScoped<DriversIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<DriversDbContext>>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<IDriverReadService, DriverReadService>();
        services.AddScoped<IDriverCredentialRepository, DriverCredentialRepository>();
        services.AddScoped<IDriverCredentialReadService, DriverCredentialReadService>();
        services.AddScoped<IDriverClearanceRepository, DriverClearanceRepository>();
        services.AddScoped<IDriverClearanceReadService, DriverClearanceReadService>();
        services.AddScoped<IHosLogRepository, HosLogRepository>();
        services.AddScoped<IHosLogReadService, HosLogReadService>();
        services.AddScoped<IDutyDayRepository, DutyDayRepository>();

        // The compliance aggregates beside the ledger, and the one reader the engine-backed
        // queries load them through.
        services.AddScoped<IHosExceptionRepository, HosExceptionRepository>();
        services.AddScoped<IDriverOutOfServiceRepository, DriverOutOfServiceRepository>();
        services.AddScoped<IEldMalfunctionRepository, EldMalfunctionRepository>();
        services.AddScoped<IComplianceFindingRepository, ComplianceFindingRepository>();
        services.AddScoped<IComplianceRecordsReader, ComplianceRecordsReader>();

        // The clock the HOS handlers take "now" from (event-in-future, certify-not-before-day-
        // starts, as-of for the rule engine). TryAdd: process-wide plumbing Trips also registers.
        services.TryAddSingleton(TimeProvider.System);

        // The caller-owns-this-row guard for the {driverId} routes on the DriverAccess and
        // HosAccess groups. Not a handler — endpoints call it BEFORE dispatching, so a denial
        // never reaches a command handler at all.
        services.AddScoped<IDriverSelfAccess, DriverSelfAccess>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<RegisterDriverCommand, Guid>, RegisterDriverCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateDriverCommand>, UpdateDriverCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeDriverStatusCommand>, ChangeDriverStatusCommandHandler>();
        services.AddScoped<IQueryHandler<GetDriversQuery, IReadOnlyList<DriverResponse>>, GetDriversQueryHandler>();
        services.AddScoped<IQueryHandler<GetDriverByIdQuery, DriverResponse>, GetDriverByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetDriverByUserQuery, DriverResponse>, GetDriverByUserQueryHandler>();
        services.AddScoped<ICommandHandler<LinkDriverUserCommand>, LinkDriverUserCommandHandler>();
        services.AddScoped<ICommandHandler<UnlinkDriverUserCommand>, UnlinkDriverUserCommandHandler>();
        services.AddScoped<ICommandHandler<AddDriverCredentialCommand, Guid>, AddDriverCredentialCommandHandler>();
        services.AddScoped<ICommandHandler<RemoveDriverCredentialCommand>, RemoveDriverCredentialCommandHandler>();
        services.AddScoped<ICommandHandler<SetDriverCredentialImageCommand>, SetDriverCredentialImageCommandHandler>();
        services.AddScoped<IQueryHandler<GetDriverCredentialsQuery, IReadOnlyList<DriverCredentialResponse>>, GetDriverCredentialsQueryHandler>();
        services.AddScoped<ICommandHandler<GrantDriverClearanceCommand, Guid>, GrantDriverClearanceCommandHandler>();
        services.AddScoped<ICommandHandler<RevokeDriverClearanceCommand>, RevokeDriverClearanceCommandHandler>();
        services.AddScoped<IQueryHandler<GetDriverClearancesQuery, IReadOnlyList<DriverClearanceResponse>>, GetDriverClearancesQueryHandler>();
        services.AddScoped<ICommandHandler<RecordHosEntryCommand, Guid>, RecordHosEntryCommandHandler>();
        services.AddScoped<IQueryHandler<GetHosEntriesQuery, IReadOnlyList<HosEntryResponse>>, GetHosEntriesQueryHandler>();

        // The duty-event ledger (DutyDay) and the rule-engine reads over it.
        services.AddScoped<ICommandHandler<RecordDutyEventCommand, DutyEventRecordedResponse>, RecordDutyEventCommandHandler>();
        services.AddScoped<ICommandHandler<AmendDutyEventCommand, DutyEventAmendedResponse>, AmendDutyEventCommandHandler>();
        services.AddScoped<ICommandHandler<TranscribeDutyDayCommand, TranscriptionRecordedResponse>, TranscribeDutyDayCommandHandler>();
        services.AddScoped<ICommandHandler<CertifyDutyDayCommand>, CertifyDutyDayCommandHandler>();
        services.AddScoped<IQueryHandler<GetDutyDaysQuery, IReadOnlyList<DutyDaySummaryResponse>>, GetDutyDaysQueryHandler>();
        services.AddScoped<IQueryHandler<GetDutyDayQuery, DutyDaySummaryResponse>, GetDutyDayQueryHandler>();
        services.AddScoped<IQueryHandler<GetHosStatusQuery, HosStatusResponse>, GetHosStatusQueryHandler>();
        services.AddScoped<IQueryHandler<GetHosRosterStatusQuery, IReadOnlyList<HosRosterStatusResponse>>, GetHosRosterStatusQueryHandler>();

        // The compliance aggregates: exceptions, out-of-service, ELD malfunctions, findings,
        // the prior-records attestation, and the audit.
        services.AddScoped<ICommandHandler<DeclareHosExceptionCommand, HosExceptionResponse>, DeclareHosExceptionCommandHandler>();
        services.AddScoped<ICommandHandler<ApproveOffDutyDeferralCommand, HosExceptionResponse>, ApproveOffDutyDeferralCommandHandler>();
        services.AddScoped<ICommandHandler<AcknowledgeHosExceptionCommand, HosExceptionResponse>, AcknowledgeHosExceptionCommandHandler>();
        services.AddScoped<ICommandHandler<RejectHosExceptionCommand, HosExceptionResponse>, RejectHosExceptionCommandHandler>();
        services.AddScoped<ICommandHandler<WithdrawHosExceptionCommand, HosExceptionResponse>, WithdrawHosExceptionCommandHandler>();
        services.AddScoped<IQueryHandler<GetHosExceptionsQuery, IReadOnlyList<HosExceptionResponse>>, GetHosExceptionsQueryHandler>();
        services.AddScoped<ICommandHandler<RecordOutOfServiceCommand, OutOfServiceRecordResponse>, RecordOutOfServiceCommandHandler>();
        services.AddScoped<ICommandHandler<LiftOutOfServiceCommand, OutOfServiceRecordResponse>, LiftOutOfServiceCommandHandler>();
        services.AddScoped<IQueryHandler<GetOutOfServiceRecordsQuery, IReadOnlyList<OutOfServiceRecordResponse>>, GetOutOfServiceRecordsQueryHandler>();
        services.AddScoped<ICommandHandler<ReportEldMalfunctionCommand, EldMalfunctionResponse>, ReportEldMalfunctionCommandHandler>();
        services.AddScoped<ICommandHandler<ResolveEldMalfunctionCommand, EldMalfunctionResponse>, ResolveEldMalfunctionCommandHandler>();
        services.AddScoped<IQueryHandler<GetEldMalfunctionsQuery, IReadOnlyList<EldMalfunctionResponse>>, GetEldMalfunctionsQueryHandler>();
        services.AddScoped<ICommandHandler<OpenComplianceFindingCommand, ComplianceFindingResponse>, OpenComplianceFindingCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateComplianceFindingCommand, ComplianceFindingResponse>, UpdateComplianceFindingCommandHandler>();
        services.AddScoped<IQueryHandler<GetComplianceFindingsQuery, IReadOnlyList<ComplianceFindingResponse>>, GetComplianceFindingsQueryHandler>();
        services.AddScoped<ICommandHandler<AttestPriorRecordsCommand, PriorRecordsResponse>, AttestPriorRecordsCommandHandler>();
        services.AddScoped<IQueryHandler<GetHosAuditQuery, IReadOnlyList<HosAuditDayResponse>>, GetHosAuditQueryHandler>();

        // 4. No integration event consumers — Drivers only publishes (drivers.driver-changed);
        //    nothing upstream feeds it yet.

        // 5. Read-side projections — one worker polls drivers.event_journal and upserts the
        //    read-model rows for the aggregates the batch touched. Credential changes also
        //    refresh the parent driver row's denormalized credential fields.
        services.AddProjections<DriversDbContext>(SchemaName, registry => registry
            .Project(new DriverProjection())
            .Project(new DriverCredentialProjection())
            .Project(new DriverClearanceProjection())
            .Project(new HosLogEntryProjection())
            .Project(new DutyDayProjection())
            .Project(new HosExceptionProjection())
            .Project(new DriverOutOfServiceProjection())
            .Project(new EldMalfunctionReportProjection())
            .Project(new ComplianceFindingProjection()));

        return services;
    }
}
