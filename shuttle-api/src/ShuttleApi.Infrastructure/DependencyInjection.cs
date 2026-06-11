using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Services;
using ShuttleApi.Domain.CommunityCalendar;
using ShuttleApi.Infrastructure.Auth;
using ShuttleApi.Infrastructure.Notifications;
using ShuttleApi.Infrastructure.Persistence;
using ShuttleApi.Domain.Clients;
using ShuttleApi.Domain.Drivers;
using ShuttleApi.Domain.Locations;
using ShuttleApi.Domain.Trips;
using ShuttleApi.Domain.Users;
using ShuttleApi.Domain.Vehicles;
using ShuttleApi.Infrastructure.BackgroundJobs;
using ShuttleApi.Infrastructure.Persistence.Repositories;
using ShuttleApi.Infrastructure.Services;
using ShuttleApi.Infrastructure.Spaces;

namespace ShuttleApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientEmailTemplateRepository, ClientEmailTemplateRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IDriverRepository, DriverRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<ISavedLocationRepository, SavedLocationRepository>();
        services.AddScoped<ICommunityCalendarBlockRepository, CommunityCalendarBlockRepository>();
        services.Configure<SpacesSettings>(configuration.GetSection(SpacesSettings.SectionName));
        services.AddScoped<IFileStorageService, SpacesFileStorageService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.Configure<PostmarkSettings>(configuration.GetSection(PostmarkSettings.SectionName));
        services.AddScoped<INotificationService, PostmarkNotificationService>();
        services.AddHostedService<CutoffProcessorHostedService>();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(opts =>
        {
            opts.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
            opts.AddPolicy("DriverOrAdmin", p => p.RequireRole("Driver", "Admin"));
            opts.AddPolicy("DispatcherOrAdmin", p => p.RequireRole("Dispatcher", "Admin"));
        });

        return services;
    }
}
