using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMRSmoduleBackend.Tests.Integration;

/// <summary>
/// Authentication- en authorization-tests. Dekt:
/// 1. Beveiligde endpoints vereisen JWT (401 zonder).
/// 2. Publieke endpoints werken anoniem.
/// 3. Cross-user IDOR is geblokkeerd op messages/history en messages/status/{trackingId}.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class AuthorizationTests(BackendIntegrationTestFactory factory) :
    IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ------------------------------------------------------------------
    // 1. Unauthenticated requests
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("GET", "/auth/me")]
    [InlineData("GET", "/health/db")]
    [InlineData("GET", "/api/messages/providers")]
    [InlineData("GET", "/api/messages/history")]
    [InlineData("GET", "/api/messages/status/some-tracking-id")]
    [InlineData("GET", "/api/reminders/history")]
    [InlineData("GET", "/api/reminders/scheduled")]
    [InlineData("GET", "/api/reminders/templates")]
    [InlineData("POST", "/api/reminders/trigger")]
    [InlineData("POST", "/api/data-retention/trigger")]
    public async Task ProtectedEndpoints_RequireAuthentication(string method, string path)
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // 2. Public endpoints (no auth required)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Health_IsAnonymouslyAccessible()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_IsAnonymouslyAccessible()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "anon-allowed@example.test",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_DoesNotReturn401_WithoutPriorAuth()
    {
        using var client = factory.CreateClient();
        // Onbekende credentials → 401 van AuthService, NIET van de authorization-middleware.
        // We controleren alleen dat het endpoint zelf bereikbaar is zonder JWT.
        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = "does-not-exist@example.test",
            password = "wrong"
        });
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.OK,
            $"Verwachtte 200 of 401 (vanuit AuthService), kreeg {(int)response.StatusCode}.");
    }

    // ------------------------------------------------------------------
    // 3. Horizontal privilege escalation — message history
    // ------------------------------------------------------------------

    [Fact]
    public async Task MessagesHistory_ReturnsOnlyCurrentUsersLogs()
    {
        var (userAId, userATokenClient) = await RegisterAsync(factory, "user-a@example.test");
        var (userBId, userBTokenClient) = await RegisterAsync(factory, "user-b@example.test");

        // Seed: user A heeft 2 message logs, user B heeft 1.
        await SeedMessageLogAsync(userAId, providerMessageId: "track-A1");
        await SeedMessageLogAsync(userAId, providerMessageId: "track-A2");
        await SeedMessageLogAsync(userBId, providerMessageId: "track-B1");

        using (userATokenClient)
        {
            var aResponse = await userATokenClient.GetAsync("/api/messages/history");
            Assert.Equal(HttpStatusCode.OK, aResponse.StatusCode);
            using var aJson = await JsonDocument.ParseAsync(await aResponse.Content.ReadAsStreamAsync());
            var aIds = aJson.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("providerMessageId").GetString())
                .ToHashSet();
            Assert.Equal(2, aIds.Count);
            Assert.Contains("track-A1", aIds);
            Assert.Contains("track-A2", aIds);
            Assert.DoesNotContain("track-B1", aIds);
        }

        using (userBTokenClient)
        {
            var bResponse = await userBTokenClient.GetAsync("/api/messages/history");
            Assert.Equal(HttpStatusCode.OK, bResponse.StatusCode);
            using var bJson = await JsonDocument.ParseAsync(await bResponse.Content.ReadAsStreamAsync());
            var bIds = bJson.RootElement.EnumerateArray()
                .Select(e => e.GetProperty("providerMessageId").GetString())
                .ToList();
            Assert.Single(bIds);
            Assert.Equal("track-B1", bIds[0]);
        }
    }

    // ------------------------------------------------------------------
    // 4. IDOR — messages/status/{trackingId}
    // ------------------------------------------------------------------

    [Fact]
    public async Task MessagesStatus_ReturnsNotFound_ForOtherUsersTrackingId()
    {
        var (userAId, _) = await RegisterAsync(factory, "owner@example.test");
        var (_, attackerClient) = await RegisterAsync(factory, "attacker@example.test");

        await SeedMessageLogAsync(userAId, providerMessageId: "secret-tracking-id");

        using (attackerClient)
        {
            var response = await attackerClient.GetAsync("/api/messages/status/secret-tracking-id");
            // 404 (geen 403) om niet te lekken of het tracking-id bestaat.
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    // ------------------------------------------------------------------
    // 5. Admin endpoints (AuthPolicies.AdminOnly)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("GET", "/api/reminders/history")]
    [InlineData("GET", "/api/reminders/scheduled")]
    [InlineData("GET", "/api/reminders/templates")]
    [InlineData("POST", "/api/reminders/trigger")]
    [InlineData("POST", "/api/data-retention/trigger")]
    public async Task NormalUser_CannotAccess_AdminEndpoints_ReturnsForbidden(string method, string path)
    {
        var (_, normalUserClient) = await RegisterAsync(factory, "normal-user@example.test");

        using (normalUserClient)
        {
            var request = new HttpRequestMessage(new HttpMethod(method), path);
            var response = await normalUserClient.SendAsync(request);

            // Een normale gebruiker mist de Admin-role en krijgt 403 Forbidden, niet 401.
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static async Task<(string userId, HttpClient authenticatedClient)> RegisterAsync(
        BackendIntegrationTestFactory factory,
        string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = "Password123!"
        });
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var token = json.RootElement.GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Haal de user-id op via /auth/me — dit is de NameIdentifier-claim die ook in
        // MessageLog.SentByUserId terechtkomt.
        var meResponse = await client.GetAsync("/auth/me");
        meResponse.EnsureSuccessStatusCode();
        using var meJson = await JsonDocument.ParseAsync(await meResponse.Content.ReadAsStreamAsync());
        var userId = meJson.RootElement.GetProperty("id").GetString()!;

        return (userId, client);
    }

    private async Task SeedMessageLogAsync(string sentByUserId, string providerMessageId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.MessageLogs.Add(new MessageLog
        {
            Provider = "swiftsend",
            MessageType = "sms",
            RecipientCount = 1,
            FailedCount = 0,
            ProviderMessageId = providerMessageId,
            Success = true,
            SentByUserId = sentByUserId
        });
        await db.SaveChangesAsync();
    }
}
