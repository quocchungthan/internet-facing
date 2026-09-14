using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Shed.Services;

public interface IIdentitySessionClient
{
    Task<IdentitySessionResult> GetSessionAsync(string? cookieHeader, CancellationToken cancellationToken);
}

public sealed record IdentitySessionResult(
    bool IsAuthenticated,
    string? Name,
    string? Email,
    string? GithubLogin,
    string? LoginUrl,
    int? StatusCode,
    string? Error);

public sealed class IdentitySessionClient(IHttpClientFactory httpClientFactory, IOptions<IdentityBridgeOptions> options)
    : IIdentitySessionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IdentitySessionResult> GetSessionAsync(string? cookieHeader, CancellationToken cancellationToken)
    {
        var baseUrl = (options.Value.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new IdentitySessionResult(false, null, null, null, null, null, "Identity bridge base URL is empty.");
        }

        try
        {
            var client = httpClientFactory.CreateClient("IdentityBridge");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/session");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrWhiteSpace(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var payloadText = await response.Content.ReadAsStringAsync(cancellationToken);

            IdentitySessionPayload? payload = null;
            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(payloadText)
                && !string.IsNullOrWhiteSpace(mediaType)
                && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                payload = JsonSerializer.Deserialize<IdentitySessionPayload>(payloadText, JsonOptions);
            }

            if (!response.IsSuccessStatusCode)
            {
                return new IdentitySessionResult(
                    false,
                    null,
                    null,
                    null,
                    payload?.LoginUrl ?? BuildFallbackLoginUrl(baseUrl),
                    (int)response.StatusCode,
                    payload?.Error ?? $"Identity session endpoint returned {(int)response.StatusCode}.");
            }

            return new IdentitySessionResult(
                payload?.IsAuthenticated == true,
                payload?.Name,
                payload?.Email,
                payload?.GithubLogin,
                payload?.LoginUrl ?? BuildFallbackLoginUrl(baseUrl),
                (int)response.StatusCode,
                null);
        }
        catch (JsonException ex)
        {
            return new IdentitySessionResult(false, null, null, null, BuildFallbackLoginUrl(baseUrl), null, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return new IdentitySessionResult(false, null, null, null, BuildFallbackLoginUrl(baseUrl), null, ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            return new IdentitySessionResult(false, null, null, null, BuildFallbackLoginUrl(baseUrl), null, ex.Message);
        }
    }

    private static string BuildFallbackLoginUrl(string baseUrl)
    {
        return $"{baseUrl}/auth/login";
    }

    private sealed class IdentitySessionPayload
    {
        public bool IsAuthenticated { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? GithubLogin { get; set; }
        public string? LoginUrl { get; set; }
        public string? Error { get; set; }
    }
}


