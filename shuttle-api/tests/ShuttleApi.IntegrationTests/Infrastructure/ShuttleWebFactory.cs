using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Infrastructure.Notifications;
using ShuttleApi.Infrastructure.Persistence;

namespace ShuttleApi.IntegrationTests.Infrastructure;

public sealed class ShuttleWebFactory : WebApplicationFactory<Program>
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=shuttle_db;Username=shuttle_user;Password=shuttle_pass_dev";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["JwtSettings:Secret"] = "integration-test-secret-key-must-be-long-enough-32chars",
                ["JwtSettings:Issuer"] = "shuttle-api",
                ["JwtSettings:Audience"] = "shuttle-tablet",
                ["JwtSettings:AccessTokenExpiryMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpiryDays"] = "7",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<INotificationService>();
            services.AddScoped<INotificationService, NoOpNotificationService>();
        });
    }

    public async Task<string> GetAdminTokenAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@northernlink.com",
            password = "Admin123!"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return body!.AccessToken;
    }

    public HttpClient CreateAuthenticatedClient(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken);
}
