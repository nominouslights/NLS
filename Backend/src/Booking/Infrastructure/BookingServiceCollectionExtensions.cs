using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.IntegrationEvents.Trips;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.Tenancy;
using NorthernLink.Booking.Application.Abstractions;
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
/// no assembly scanning), and the trips.route-changed consumer that maintains the
/// corridor replica. Booking publishes no integration events yet, so there is no
/// integration event mapper and no OutboxDispatcher — its outbox table exists (shared
/// module shape) and stays empty until the first public contract (likely the
/// threshold-confirmation chain reaction in a later batch).
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

        // 2. Persistence + read services.
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerReadService, CustomerReadService>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingReadService, BookingReadService>();
        services.AddScoped<IBookingDayRepository, BookingDayRepository>();
        services.AddScoped<IBookingPolicyRepository, BookingPolicyRepository>();
        services.AddScoped<ICorridorSettingsRepository, CorridorSettingsRepository>();
        services.AddScoped<ICorridorLookupRepository, CorridorLookupRepository>();

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

        // 4. Integration event consumers — the storing/projecting path: one polling consumer
        //    over the trips outbox maintains booking.corridor_lookup. First poll replays the
        //    routing key's entire history (how the replica bootstraps), but routes saved before
        //    trips.route-changed existed never published — re-save each once (runbook step).
        services.AddOutboxPollingConsumer<BookingDbContext>(SchemaName, subscriptions => subscriptions
            .On<RouteChangedIntegrationEvent, RouteChangedIntegrationEventHandler>());

        // 5. Read-side projections — none: query handlers derive the seat math from the
        //    aggregate tables directly, so there are no rm_* tables to maintain.

        return services;
    }
}
