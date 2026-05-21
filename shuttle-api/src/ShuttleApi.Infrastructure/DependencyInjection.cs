using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Application.Services;
using ShuttleApi.Infrastructure.Auth;
using ShuttleApi.Infrastructure.Persistence;
using ShuttleApi.Domain.Users;
using ShuttleApi.Infrastructure.Persistence.Repositories;
using ShuttleApi.Infrastructure.Services;

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
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();

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
