using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Domain;
using Infrastructure.Persistence;
using Infrastructure.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMRSmoduleBackend.Tests.Integration;

public sealed class BackendIntegrationTests(
    BackendIntegrationTestFactory factory) :
    IClassFixture<BackendIntegrationTestFactory>,
    IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task HealthDb_ReturnsReachable_WhenUsingIntegrationDatabase()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/db");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("reachable", json.RootElement.GetProperty("db").GetString());
    }

    [Fact]
    public async Task AppointmentWebhook_AcceptsSignedEvent_AndExposesScheduledReminders()
    {
        using var client = factory.CreateClient();
        var start = DateTime.UtcNow.AddDays(3);
        var body = CreateAppointmentBody("enc-100", "patient-100", start, "scheduled");

        var response = await client.SendAsync(CreateWebhookRequest(
            body,
            eventId: "event-100",
            eventType: "encounter.created"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var appointment = await db.AppointmentNotifications.SingleAsync();
            var reminders = await db.ScheduledReminders
                .OrderBy(r => r.ReminderWindow)
                .ToListAsync();

            Assert.Equal("enc-100", appointment.EncounterId);
            Assert.False(appointment.IsCancelled);
            Assert.Equal("event-100", appointment.LastEventId);
            Assert.Equal(2, reminders.Count);
            Assert.All(reminders, reminder =>
            {
                Assert.Equal(ScheduledReminderStatus.Pending, reminder.Status);
                Assert.Equal("SwiftSend", reminder.Provider);
            });
        }

        await AuthenticateAsync(client, "reminder-reader@example.test");

        var scheduledResponse = await client.GetAsync("/api/reminders/scheduled");

        Assert.Equal(HttpStatusCode.OK, scheduledResponse.StatusCode);
        using var scheduledJson = await JsonDocument.ParseAsync(
            await scheduledResponse.Content.ReadAsStreamAsync());
        Assert.Equal(2, scheduledJson.RootElement.GetArrayLength());
        Assert.All(scheduledJson.RootElement.EnumerateArray(), reminder =>
        {
            Assert.Equal("enc-100", reminder.GetProperty("encounterId").GetString());
            Assert.Equal("pending", reminder.GetProperty("status").GetString());
        });
    }

    [Fact]
    public async Task AppointmentWebhook_IsIdempotent_ForDuplicateEventIds()
    {
        using var client = factory.CreateClient();
        var start = DateTime.UtcNow.AddDays(3);
        var body = CreateAppointmentBody("enc-200", "patient-200", start, "scheduled");

        var first = await client.SendAsync(CreateWebhookRequest(
            body,
            eventId: "event-200",
            eventType: "encounter.created"));
        var second = await client.SendAsync(CreateWebhookRequest(
            body,
            eventId: "event-200",
            eventType: "encounter.created"));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.WebhookEventLogs.CountAsync());
        Assert.Equal(1, await db.AppointmentNotifications.CountAsync());
        Assert.Equal(2, await db.ScheduledReminders.CountAsync());
    }

    [Fact]
    public async Task AppointmentWebhook_RejectsInvalidSignature()
    {
        using var client = factory.CreateClient();
        var body = CreateAppointmentBody(
            "enc-invalid-signature",
            "patient-invalid-signature",
            DateTime.UtcNow.AddDays(3),
            "scheduled");
        var request = CreateWebhookRequest(
            body,
            eventId: "event-invalid-signature",
            eventType: "encounter.created");
        request.Headers.Remove("X-OpenMRS-Signature");
        request.Headers.Add("X-OpenMRS-Signature", "sha256=deadbeef");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.WebhookEventLogs.ToListAsync());
        Assert.Empty(await db.AppointmentNotifications.ToListAsync());
        Assert.Empty(await db.ScheduledReminders.ToListAsync());
    }

    private static string CreateAppointmentBody(
        string encounterId,
        string patientId,
        DateTime startUtc,
        string status) =>
        JsonSerializer.Serialize(new
        {
            encounterId,
            patientId,
            start = startUtc.ToString("O"),
            status,
            patientDisplay = "Patient Name Should Not Be Logged",
            serviceType = "Controle",
            location = "Polikliniek",
            instructions = "Kom tien minuten eerder."
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static HttpRequestMessage CreateWebhookRequest(
        string body,
        string eventId,
        string eventType)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = OpenMrsWebhookSignatureValidator.ComputeSignatureHex(
            timestamp,
            body,
            BackendIntegrationTestFactory.OpenMrsWebhookSecret);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/webhooks/openmrs/appointments")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-OpenMRS-Event-Id", eventId);
        request.Headers.Add("X-OpenMRS-Event-Type", eventType);
        request.Headers.Add("X-OpenMRS-Timestamp", timestamp);
        request.Headers.Add("X-OpenMRS-Organization-Id", "lu1");
        request.Headers.Add("X-OpenMRS-Signature", $"sha256={signature}");
        return request;
    }

    private static async Task AuthenticateAsync(HttpClient client, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = "Password123!"
        });
        registerResponse.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await registerResponse.Content.ReadAsStreamAsync());
        var token = json.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
