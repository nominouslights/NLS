using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Application.BookingDays;
using NorthernLink.Booking.Application.BookingDays.Guarantee;
using NorthernLink.Booking.Application.Bookings;
using NorthernLink.Booking.Application.Bookings.Cancel;
using NorthernLink.Booking.Application.Bookings.Confirm;
using NorthernLink.Booking.Application.Bookings.Create;
using NorthernLink.Booking.Application.Bookings.Update;
using NorthernLink.Booking.Application.Calendar.GetDay;
using NorthernLink.Booking.Application.Calendar.GetMonth;
using NorthernLink.Booking.Application.Corridors.GetCorridors;
using NorthernLink.Booking.Application.Customers;
using NorthernLink.Booking.Application.Customers.Create;
using NorthernLink.Booking.Application.Customers.GetById;
using NorthernLink.Booking.Application.Customers.Search;
using NorthernLink.Booking.Application.Customers.Update;
using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Application.Settings.GetSettings;
using NorthernLink.Booking.Application.Settings.SetDayOverrides;
using NorthernLink.Booking.Application.Settings.UpdatePolicy;
using NorthernLink.Booking.Application.Settings.UpsertCorridorSettings;
using NorthernLink.Booking.Infrastructure.Persistence;

namespace NorthernLink.Booking.Infrastructure;

/// <summary>
/// DI entry point for the Booking domain library — the only thing the API gateway sees.
/// Registers the library DbContext (Postgres schema "booking"), persistence services,
/// every CQRS handler explicitly (the reflection-based Sender resolves handlers from DI;
/// no assembly scanning), the module's integration event mapper + outbox dispatcher
/// (Booking now publishes: the booking-day-confirmed chain reaction over RabbitMQ and the
/// booking-day-reverted storing event), and the polling consumers that maintain the
/// corridor replica and the day↔trip backlink.
/// </summary>
public static class BookingServiceCollectionExtensions
{
    public const string SchemaName = "booking";

    public static IServiceCollection AddBooking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 1. DbContext — module schema, tenant session interceptor (RLS session variable).
        //    TryAdd: the interceptor is shared platform plumbing other modules also register.
        services.TryAddScoped<TenantSessionInterceptor>();

        services.AddDbContext<BookingDbContext>((serviceProvider, options) =>
            options
                .UseNpgsql(
                    RequiredEnvironmentVariable.Get("ConnectionStrings__Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", SchemaName))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantSessionInterceptor>()));

        // 2. Persistence + read services. The mapper is registered as its concrete type —
        //    every module has its own IIntegrationEventMapper, so the interface can't be a
        //    single DI registration. The outbox dispatcher publishes the module's
        //    bus-designated rows (booking.booking-day-confirmed) to RabbitMQ; the reverted
        //    event stays on the polling path and needs no dispatcher involvement.
        services.AddScoped<BookingIntegrationEventMapper>();
        services.AddHostedService<OutboxDispatcher<BookingDbContext>>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerReadService, CustomerReadService>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingReadService, BookingReadService>();
        services.AddScoped<IBookingDayRepository, BookingDayRepository>();
        services.AddScoped<IBookingPolicyRepository, BookingPolicyRepository>();
        services.AddScoped<ICorridorSettingsRepository, CorridorSettingsRepository>();
        services.AddScoped<ICorridorLookupRepository, CorridorLookupRepository>();

        // Threshold recompute (confirm/cancel handlers run it inside their transaction) and
        // the injectable clock the 12-hour window rule is tested through. TryAdd: the
        // system clock is process-wide plumbing another module may also register.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<BookingDayThresholdService>();

        // 3. Command/query handlers — registered explicitly, one line per handler.
        services.AddScoped<ICommandHandler<CreateCustomerCommand, Guid>, CreateCustomerCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateCustomerCommand>, UpdateCustomerCommandHandler>();
        services.AddScoped<IQueryHandler<SearchCustomersQuery, IReadOnlyList<CustomerResponse>>, SearchCustomersQueryHandler>();
        services.AddScoped<IQueryHandler<GetCustomerByIdQuery, CustomerResponse>, GetCustomerByIdQueryHandler>();
        services.AddScoped<ICommandHandler<CreateBookingCommand, Guid>, CreateBookingCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateBookingCommand>, UpdateBookingCommandHandler>();
        services.AddScoped<ICommandHandler<ConfirmBookingCommand>, ConfirmBookingCommandHandler>();
        services.AddScoped<ICommandHandler<CancelBookingCommand>, CancelBookingCommandHandler>();
        services.AddScoped<IQueryHandler<GetBookingCalendarMonthQuery, IReadOnlyList<CalendarDaySummaryResponse>>, GetBookingCalendarMonthQueryHandler>();
        services.AddScoped<IQueryHandler<GetBookingDayDetailQuery, BookingDayDetailResponse>, GetBookingDayDetailQueryHandler>();
        services.AddScoped<IQueryHandler<GetCorridorsQuery, IReadOnlyList<CorridorResponse>>, GetCorridorsQueryHandler>();
        services.AddScoped<IQueryHandler<GetBookingSettingsQuery, BookingSettingsResponse>, GetBookingSettingsQueryHandler>();
        services.AddScoped<ICommandHandler<UpdateBookingPolicyCommand>, UpdateBookingPolicyCommandHandler>();
        services.AddScoped<ICommandHandler<UpsertCorridorBookingSettingsCommand>, UpsertCorridorBookingSettingsCommandHandler>();
        services.AddScoped<ICommandHandler<SetBookingDayOverridesCommand>, SetBookingDayOverridesCommandHandler>();
        services.AddScoped<ICommandHandler<GuaranteeBookingDayCommand>, GuaranteeBookingDayCommandHandler>();

        // 4. Integration event consumers — the storing/projecting path: one polling consumer
        //    over the trips outbox maintains booking.corridor_lookup and stamps the
        //    day↔trip backlink. First poll replays each routing key's entire history (how
        //    the replica bootstraps), but routes saved before trips.route-changed existed
        //    never published — re-save each once (runbook step).
        services.AddOutboxPollingConsumer<BookingDbContext>(SchemaName, subscriptions => subscriptions
            .On<RouteChangedIntegrationEvent, RouteChangedIntegrationEventHandler>()
            .On<TripScheduledFromBookingIntegrationEvent, TripScheduledFromBookingIntegrationEventHandler>());

        // 5. Read-side projections — none: query handlers derive the seat math from the
        //    aggregate tables directly, so there are no rm_* tables to maintain.

        return services;
    }
}
