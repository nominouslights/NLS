using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Drivers.Infrastructure;
using NorthernLink.Drivers.Infrastructure.Motive;
using NorthernLink.Shared.Kernel;
using Xunit;

namespace NorthernLink.Drivers.Tests.Motive;

/// <summary>
/// The Motive credential seam. Pure unit tests — no HTTP is sent and the process environment is
/// never mutated: the secret reader is substituted, which is also how the tests prove the key is
/// read lazily (on <c>ApplyAsync</c>, never at construction or DI registration).
/// </summary>
public class ApiKeyMotiveCredentialProviderTests
{
    private static HttpRequestMessage NewRequest() =>
        new(HttpMethod.Get, "https://api.gomotive.com/v1/users");

    [Fact]
    public async Task Applies_the_key_as_the_X_API_Key_header_read_from_Motive__ApiKey()
    {
        var asked = new List<string>();
        var provider = new ApiKeyMotiveCredentialProvider
        {
            ReadSecret = name =>
            {
                asked.Add(name);
                return "test-key-123";
            },
        };
        using var request = NewRequest();

        await provider.ApplyAsync(request);

        Assert.Equal(["Motive__ApiKey"], asked);
        Assert.Equal(["test-key-123"], request.Headers.GetValues("X-API-Key"));
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task Applying_twice_replaces_rather_than_duplicates_the_header()
    {
        var key = "first";
        var provider = new ApiKeyMotiveCredentialProvider { ReadSecret = _ => key };
        using var request = NewRequest();

        await provider.ApplyAsync(request);
        key = "rotated";
        await provider.ApplyAsync(request);

        // Read per call, so a rotated key takes effect without a restart.
        Assert.Equal(["rotated"], request.Headers.GetValues("X-API-Key"));
    }

    [Fact]
    public void Constructing_reads_nothing()
    {
        var reads = 0;

        _ = new ApiKeyMotiveCredentialProvider { ReadSecret = _ => { reads++; return "k"; } };

        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task A_missing_key_fails_on_first_use_naming_the_variable_and_leaves_the_request_untouched()
    {
        var provider = new ApiKeyMotiveCredentialProvider
        {
            ReadSecret = name => throw new InvalidOperationException($"The {name} environment variable is not set."),
        };
        using var request = NewRequest();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.ApplyAsync(request).AsTask());

        Assert.Contains("Motive__ApiKey", thrown.Message);
        Assert.False(request.Headers.Contains("X-API-Key"));
    }

    [Fact]
    public void The_default_reader_is_the_environment_only_secret_reader()
    {
        // Never IConfiguration/appsettings: the key can only come from the process environment.
        Func<string, string> expected = RequiredEnvironmentVariable.Get;

        Assert.Equal(expected, new ApiKeyMotiveCredentialProvider().ReadSecret);
    }

    [Fact]
    public async Task A_null_request_is_rejected()
    {
        var provider = new ApiKeyMotiveCredentialProvider { ReadSecret = _ => "k" };

        await Assert.ThrowsAsync<ArgumentNullException>(() => provider.ApplyAsync(null!).AsTask());
    }

    [Fact]
    public void AddDrivers_registers_the_api_key_provider_and_resolving_it_reads_no_secret()
    {
        // The API must boot without Motive__ApiKey: registration and resolution touch no
        // environment variable (the DbContext's connection string is read only when a context is
        // built, which this never does).
        var services = new ServiceCollection();
        services.AddDrivers(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var credentials = provider.GetRequiredService<IMotiveCredentialProvider>();

        Assert.IsType<ApiKeyMotiveCredentialProvider>(credentials);
        Assert.Same(credentials, provider.GetRequiredService<IMotiveCredentialProvider>());
    }
}
