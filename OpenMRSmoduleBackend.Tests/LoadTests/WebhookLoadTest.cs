using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NBomber.CSharp;
using NBomber.Contracts;

namespace OpenMRSmoduleBackend.Tests.LoadTests;

/// <summary>
/// Load test die 50 gelijktijdige gebruikers simuleert die HMAC-gesignde webhooks
/// naar de draaiende Docker-stack sturen. Vereist dat de stack draait op localhost:5111.
///
/// Draai expliciet met:  dotnet test --filter "Category=LoadTest"
/// </summary>
[Trait("Category", "LoadTest")]
public class WebhookLoadTest
{
    private const string BaseUrl = "http://localhost:5111";
    private const string WebhookPath = "/api/webhooks/openmrs/appointments";
    private const string WebhookSecret = "8evzDLKtK2W9n2Wc/6Ul3EV+9fZob7fscRJ9vycKei0=";
    private const string OrganizationId = "lu1";

    [Fact]
    public void Webhook_LoadTest_50VUs_60Seconds()
    {
        var counter = 0;

        var scenario = Scenario.Create("webhook_ingestion", async context =>
        {
            var id = Interlocked.Increment(ref counter);
            var encounterId = $"load-enc-{id}";
            var eventId = $"load-evt-{id}";
            var patientId = $"load-patient-{id}";
            var start = DateTime.UtcNow.AddDays(3).ToString("o");

            var body = JsonSerializer.Serialize(new
            {
                encounterId,
                patientId,
                start,
                status = "scheduled",
                patientDisplay = $"Testpatient {id}",
                location = "Polikliniek A"
            });

            var timestamp = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            var signature = ComputeSignature(timestamp, body, WebhookSecret);

            using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.Add("X-OpenMRS-Event-Id", eventId);
            request.Headers.Add("X-OpenMRS-Event-Type", "encounter.created");
            request.Headers.Add("X-OpenMRS-Organization-Id", OrganizationId);
            request.Headers.Add("X-OpenMRS-Timestamp", timestamp);
            request.Headers.Add("X-OpenMRS-Signature", $"sha256={signature}");

            using var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
            var response = await httpClient.SendAsync(request);

            return (int)response.StatusCode < 400
                ? Response.Ok(statusCode: ((int)response.StatusCode).ToString())
                : Response.Fail(statusCode: ((int)response.StatusCode).ToString());
        })
        .WithLoadSimulations(
            Simulation.Inject(rate: 50, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(60))
        );

        var result = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("reports")
            .Run();

        // Sanity check: minstens 80% van de requests moet slagen
        var stats = result.ScenarioStats[0];
        var successRate = stats.Ok.Request.Count / (double)stats.AllRequestCount * 100;
        Assert.True(successRate > 80, $"Slechts {successRate:F1}% van de requests slaagde.");
    }

    private static string ComputeSignature(string timestamp, string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
        return Convert.ToHexString(hmac.ComputeHash(bytes)).ToLowerInvariant();
    }
}
