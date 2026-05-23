using System.Text;
using System.Xml.Linq;
using Application.Messaging;
using Infrastructure.Messaging.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Messaging.Providers;

public class LegacyLinkProvider : IMessageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LegacyLinkOptions _options;
    private readonly string _studentGroup;
    private readonly string _basicAuth;

    public string ProviderName => "legacylink";

    public LegacyLinkProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<LegacyLinkOptions> options,
        IOptions<MessagingOptions> messagingOptions)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _studentGroup = messagingOptions.Value.StudentGroup;
        _basicAuth = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
    }

    public async Task<SendMessageResult> SendAsync(SendMessageRequest request, CancellationToken ct = default)
    {
        var failedRecipients = new List<string>();
        string? lastReference = null;
        string? lastError = null;

        var ns = XNamespace.Get("http://legacylink.fakecomworld.com/v1");

        foreach (var recipient in request.Recipients)
        {
            try
            {
                var doc = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement(ns + "SendSmsRequest",
                        new XElement(ns + "PhoneNumber", recipient),
                        new XElement(ns + "MessageText", request.Content),
                        new XElement(ns + "SenderIdentification", "OpenMRS")));

                var client = _httpClientFactory.CreateClient();
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/LegacyLink/SendSms");
                httpRequest.Headers.Add("Authorization", $"Basic {_basicAuth}");
                httpRequest.Headers.Add("X-STUDENT-GROUP", _studentGroup);
                httpRequest.Headers.Add("Accept", "application/xml");
                httpRequest.Content = new StringContent(doc.ToString(), Encoding.UTF8, "application/xml");

                var response = await client.SendAsync(httpRequest, ct);
                var responseXml = await response.Content.ReadAsStringAsync(ct);

                var responseDoc = XDocument.Parse(responseXml);
                var statusCode = responseDoc.Root?.Element(ns + "StatusCode")?.Value;
                var reference = responseDoc.Root?.Element(ns + "MessageReference")?.Value;
                var statusMessage = responseDoc.Root?.Element(ns + "StatusMessage")?.Value;

                if (statusCode == "200")
                    lastReference = reference;
                else
                {
                    failedRecipients.Add(recipient);
                    lastError = statusMessage ?? "Unknown error";
                }
            }
            catch (Exception ex)
            {
                failedRecipients.Add(recipient);
                lastError = ex.Message;
            }
        }

        var success = failedRecipients.Count == 0;
        return new SendMessageResult(success, lastReference, success ? null : lastError, [.. failedRecipients]);
    }
}
