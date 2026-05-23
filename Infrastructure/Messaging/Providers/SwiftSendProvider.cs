using System.Net.Http.Json;
using Application.Messaging;
using Infrastructure.Messaging.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Providers;

public class SwiftSendProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<SwiftSendOptions> options,
    IOptions<MessagingOptions> messagingOptions) : IMessageProvider
{
    private readonly SwiftSendOptions _options = options.Value;
    private readonly string _studentGroup = messagingOptions.Value.StudentGroup;

    public string ProviderName => "swiftsend";

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/swiftsend");
            httpRequest.Headers.Add("X-API-KEY", _options.ApiKey);
            httpRequest.Headers.Add("X-STUDENT-GROUP", _studentGroup);
            httpRequest.Content = JsonContent.Create(new
            {
                type = request.Type,
                recipients = request.Recipients,
                content = request.Content
            });

            var response = await client.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadFromJsonAsync<SwiftSendResponse>(cancellationToken: ct);

            if (body is null)
                return new SendMessageResult(false, null, "Empty response", []);

            return new SendMessageResult(body.Success, body.MessageId, body.Error, body.FailedRecipients ?? []);
        }
        catch (Exception ex)
        {
            return new SendMessageResult(false, null, ex.Message, []);
        }
    }

    private record SwiftSendResponse(bool Success, string? MessageId, string[]? FailedRecipients, string? Error);
}
