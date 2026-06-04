using System.Net.Http.Json;
using Application.Messaging;
using Infrastructure.Messaging.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Providers;

public class AsyncFlowProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AsyncFlowOptions> options,
    IOptions<MessagingOptions> messagingOptions) : IAsyncMessageProvider
{
    private readonly AsyncFlowOptions _options = options.Value;
    private readonly string _studentGroup = messagingOptions.Value.StudentGroup;

    public string ProviderName => "asyncflow";

    public async Task<SendMessageResult> SendAsync(
        SendMessageRequest request,
        MessageProviderConfiguration? configuration = null,
        CancellationToken ct = default)
    {
        var failedRecipients = new List<string>();
        string? trackingId = null;
        string? lastError = null;
        var baseUrl = configuration?.BaseUrl ?? _options.BaseUrl;
        var apiKey = configuration?.GetCredential("apiKey") ?? _options.ApiKey;
        var studentGroup = configuration?.StudentGroup ?? _studentGroup;

        foreach (var recipient in request.Recipients)
        {
            try
            {
                var client = httpClientFactory.CreateClient();
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/asyncflow");
                httpRequest.Headers.Add("X-API-KEY", apiKey);
                httpRequest.Headers.Add("X-STUDENT-GROUP", studentGroup);
                httpRequest.Content = JsonContent.Create(new
                {
                    destination = recipient,
                    content = request.Content,
                    priority = "normal"
                });

                var response = await client.SendAsync(httpRequest, ct);
                if (!response.IsSuccessStatusCode)
                {
                    failedRecipients.Add(recipient);
                    lastError = $"HTTP {(int)response.StatusCode}";
                    continue;
                }

                var body = await response.Content.ReadFromJsonAsync<AsyncFlowSubmitResponse>(cancellationToken: ct);
                if (body is { Accepted: true })
                    trackingId = body.TrackingId;
                else
                {
                    failedRecipients.Add(recipient);
                    lastError = body?.Message ?? "Not accepted";
                }
            }
            catch (Exception ex)
            {
                failedRecipients.Add(recipient);
                lastError = ex.Message;
            }
        }

        var success = failedRecipients.Count == 0;
        return new SendMessageResult(success, trackingId, success ? null : lastError, [.. failedRecipients]);
    }

    public async Task<MessageStatusResult> GetStatusAsync(
        string trackingId,
        MessageProviderConfiguration? configuration = null,
        CancellationToken ct = default)
    {
        var baseUrl = configuration?.BaseUrl ?? _options.BaseUrl;
        var apiKey = configuration?.GetCredential("apiKey") ?? _options.ApiKey;
        var studentGroup = configuration?.StudentGroup ?? _studentGroup;
        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/asyncflow/{trackingId}");
        request.Headers.Add("X-API-KEY", apiKey);
        request.Headers.Add("X-STUDENT-GROUP", studentGroup);

        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<AsyncFlowStatusResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty status response from AsyncFlow");

        return new MessageStatusResult(body.TrackingId, body.Status, body.SubmittedAt, body.ProcessedAt, body.ErrorDetails);
    }

    private record AsyncFlowSubmitResponse(bool Accepted, string TrackingId, string Message, DateTime SubmittedAt);
    private record AsyncFlowStatusResponse(string TrackingId, string Status, DateTime? SubmittedAt, DateTime? ProcessedAt, string? ErrorDetails);
}
