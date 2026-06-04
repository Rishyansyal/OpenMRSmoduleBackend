using System.Net;
using System.Net.Http.Json;
using Application.Messaging;
using Infrastructure.Messaging.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Providers;

/// <summary>
/// Singleton provider voor het SecurePost messaging platform.
/// Beheert een OAuth 2.0 bearer-token met thread-veilige invalidatie via SemaphoreSlim.
/// </summary>
// Singleton lifetime: token cache moet worden gedeeld over requests.
public class SecurePostProvider : IMessageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SecurePostOptions _options;
    private readonly string _studentGroup;

    // volatile zorgt voor memory-visibility: writes in lock zijn zichtbaar in de fast-path check
    private volatile string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public string ProviderName => "securepost";

    public SecurePostProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<SecurePostOptions> options,
        IOptions<MessagingOptions> messagingOptions)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _studentGroup      = messagingOptions.Value.StudentGroup;
    }

    private async Task<string?> GetTokenAsync(
        MessageProviderConfiguration? configuration,
        CancellationToken ct)
    {
        if (configuration is not null)
        {
            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{configuration.BaseUrl}/securepost/auth");
            request.Headers.Add("X-STUDENT-GROUP", configuration.StudentGroup);
            request.Content = JsonContent.Create(new
            {
                clientId = configuration.GetCredential("clientId"),
                clientSecret = configuration.GetCredential("clientSecret")
            });

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            return tokenResponse?.AccessToken;
        }

        // Fast path: token nog geldig (geen lock nodig)
        if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            // Double-check na het verkrijgen van de lock
            if (_cachedToken is not null && DateTime.UtcNow < _tokenExpiresAt)
                return _cachedToken;

            var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/securepost/auth");
            request.Headers.Add("X-STUDENT-GROUP", _studentGroup);
            request.Content = JsonContent.Create(new
            {
                clientId     = _options.ClientId,
                clientSecret = _options.ClientSecret
            });

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            if (tokenResponse is null) return null;

            _cachedToken = tokenResponse.AccessToken;
            // 10s buffer voorkomt gebruik van bijna-verlopen token
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 10);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// Invalideert de token-cache op een thread-veilige manier.
    /// De invalidatie vindt altijd binnen de lock plaats om race conditions te voorkomen.
    /// </summary>
    private async Task InvalidateTokenAsync(CancellationToken ct)
    {
        await _tokenLock.WaitAsync(ct);
        try
        {
            _cachedToken    = null;
            _tokenExpiresAt = DateTime.MinValue;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<SendMessageResult> SendAsync(
        SendMessageRequest request,
        MessageProviderConfiguration? configuration = null,
        CancellationToken ct = default)
    {
        var failedRecipients = new List<string>();
        string? lastTrackingId = null;
        string? lastError = null;

        foreach (var recipient in request.Recipients)
        {
            var (success, trackingId, error) = await SendSingleAsync(recipient, request, configuration, ct);
            if (success)
                lastTrackingId = trackingId;
            else
            {
                failedRecipients.Add(recipient);
                lastError = error;
            }
        }

        var allSucceeded = failedRecipients.Count == 0;
        return new SendMessageResult(allSucceeded, lastTrackingId, allSucceeded ? null : lastError, [.. failedRecipients]);
    }

    private async Task<(bool Success, string? TrackingId, string? Error)> SendSingleAsync(
        string recipient,
        SendMessageRequest request,
        MessageProviderConfiguration? configuration,
        CancellationToken ct)
    {
        string? token = await GetTokenAsync(configuration, ct);
        if (token is null) return (false, null, "Failed to obtain SecurePost token");
        var baseUrl = configuration?.BaseUrl ?? _options.BaseUrl;
        var studentGroup = configuration?.StudentGroup ?? _studentGroup;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var client = _httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/securepost/message");
            httpRequest.Headers.Add("Authorization", $"Bearer {token}");
            httpRequest.Headers.Add("X-STUDENT-GROUP", studentGroup);
            httpRequest.Content = JsonContent.Create(new
            {
                format    = request.Type,
                recipient,
                body      = request.Content,
                subject   = request.Subject
            });

            var response = await client.SendAsync(httpRequest, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                // Invalideert token BINNEN lock om race condition te voorkomen
                if (configuration is null)
                    await InvalidateTokenAsync(ct);
                token = await GetTokenAsync(configuration, ct);
                if (token is null) return (false, null, "Token refresh failed");
                continue;
            }

            if (!response.IsSuccessStatusCode)
                return (false, null, $"HTTP {(int)response.StatusCode}");

            var body = await response.Content.ReadFromJsonAsync<SecurePostResponse>(cancellationToken: ct);
            return body is { Delivered: true }
                ? (true, body.TrackingId, null)
                : (false, null, body?.ErrorMessage ?? "Delivery failed");
        }

        return (false, null, "Max retries exceeded");
    }

    private record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, DateTime IssuedAt);
    private record SecurePostResponse(bool Delivered, string? TrackingId, string? ErrorMessage, DateTime? DeliveryTimestamp);
}
