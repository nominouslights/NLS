using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NorthernLink.Budgeting.Application.Qbo;

namespace NorthernLink.Budgeting.Infrastructure.Qbo;

/// <summary>
/// <see cref="IQboAccountingClient"/> over the QuickBooks Accounting API v3 — raw JSON through a
/// typed <see cref="HttpClient"/> whose base address is <see cref="QboOptions.ApiBaseUrl"/>.
/// GET only: the platform reads QuickBooks and never writes to it. Every call pins
/// <c>minorversion</c>. The realm id is checked to be digits before it is put in a path (the
/// aggregate's rule), and the bearer token never appears in an exception.
/// </summary>
public sealed class QboAccountingClient(HttpClient httpClient, QboOptions options) : IQboAccountingClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public string Environment => options.Environment.ToString();

    public async Task<QboCompanyInfo> GetCompanyInfoAsync(
        string realmId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var body = await GetAsync<CompanyInfoEnvelope>(realmId, $"companyinfo/{realmId}", accessToken, "companyinfo", cancellationToken);

        if (body.CompanyInfo is not { } info)
        {
            throw new QboApiException(QboApiFailure.Rejected, "QuickBooks companyinfo returned no CompanyInfo.");
        }

        return new QboCompanyInfo(info.CompanyName ?? string.Empty, info.Country);
    }

    public async Task<string?> GetHomeCurrencyAsync(
        string realmId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var body = await GetAsync<PreferencesEnvelope>(realmId, "preferences", accessToken, "preferences", cancellationToken);
        return body.Preferences?.CurrencyPrefs?.HomeCurrency?.Value;
    }

    private async Task<T> GetAsync<T>(
        string realmId,
        string resource,
        string accessToken,
        string operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(realmId) || !realmId.All(char.IsAsciiDigit))
        {
            throw new QboApiException(QboApiFailure.Rejected, "The QuickBooks realm id must be digits only.");
        }

        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"/v3/company/{realmId}/{resource}?minorversion={options.MinorVersion}");

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new QboApiException(QboApiFailure.Unreachable, $"QuickBooks {operation} could not be reached.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var failure = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => QboApiFailure.Unauthorized,
                    HttpStatusCode.TooManyRequests => QboApiFailure.Throttled,
                    _ => QboApiFailure.Rejected,
                };

                throw new QboApiException(failure, $"QuickBooks {operation} failed with HTTP {(int)response.StatusCode}.");
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken)
                    ?? throw new QboApiException(QboApiFailure.Rejected, $"QuickBooks {operation} returned an empty body.");
            }
            catch (JsonException)
            {
                throw new QboApiException(QboApiFailure.Rejected, $"QuickBooks {operation} returned a body that is not valid JSON.");
            }
        }
    }

    private sealed record CompanyInfoEnvelope([property: JsonPropertyName("CompanyInfo")] CompanyInfoBody? CompanyInfo);

    private sealed record CompanyInfoBody(
        [property: JsonPropertyName("CompanyName")] string? CompanyName,
        [property: JsonPropertyName("Country")] string? Country);

    private sealed record PreferencesEnvelope([property: JsonPropertyName("Preferences")] PreferencesBody? Preferences);

    private sealed record PreferencesBody([property: JsonPropertyName("CurrencyPrefs")] CurrencyPrefsBody? CurrencyPrefs);

    private sealed record CurrencyPrefsBody([property: JsonPropertyName("HomeCurrency")] ReferenceValue? HomeCurrency);

    private sealed record ReferenceValue([property: JsonPropertyName("value")] string? Value);
}
