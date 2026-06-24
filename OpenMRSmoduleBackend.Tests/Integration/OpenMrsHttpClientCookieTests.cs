using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMRSmoduleBackend.Tests.Integration;

/// <summary>
/// Regressietest voor de multi-OpenMRS cookie-isolatie. Meerdere OpenMRS-instanties draaien
/// op dezelfde host (host.docker.internal, andere poort) en cookies negeren de poort. Als de
/// gedeelde "openmrs"-HttpClient een cookie-jar had, zou de JSESSIONID van organisatie A
/// meelekken naar organisatie B en daar een 401 veroorzaken. De client moet daarom cookies
/// uitstaan hebben (Program.cs: ConfigurePrimaryHttpMessageHandler met UseCookies = false).
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class OpenMrsHttpClientCookieTests(BackendIntegrationTestFactory factory)
{
    [Fact]
    public async Task OpenMrsClient_DoesNotResendCookieOnNextRequest()
    {
        var httpClientFactory = factory.Services.GetRequiredService<IHttpClientFactory>();
        using var openMrsClient = httpClientFactory.CreateClient("openmrs");

        var cookieHeaders = await ProbeCookiePersistenceAsync(openMrsClient);

        // De server zet bij elk antwoord een cookie; de client mag die op geen enkel
        // volgend verzoek terugsturen.
        Assert.Null(cookieHeaders[0]);
        Assert.Null(cookieHeaders[1]);
    }

    [Fact]
    public async Task ControlClientWithCookiesEnabled_DoesResendCookie()
    {
        // Sanity-check: bewijst dat de probe cookie-lekkage daadwerkelijk detecteert,
        // zodat de test hierboven niet vals-positief kan slagen.
        using var cookieClient = new HttpClient(new HttpClientHandler { UseCookies = true });

        var cookieHeaders = await ProbeCookiePersistenceAsync(cookieClient);

        Assert.Null(cookieHeaders[0]);          // eerste verzoek: nog geen cookie
        Assert.NotNull(cookieHeaders[1]);       // tweede verzoek: cookie wordt wél meegestuurd
    }

    /// <summary>
    /// Doet twee opeenvolgende GET-verzoeken naar een lokale testserver die bij elk antwoord
    /// een Set-Cookie meestuurt, en geeft per verzoek de ontvangen Cookie-header terug.
    /// </summary>
    private static async Task<IReadOnlyList<string?>> ProbeCookiePersistenceAsync(HttpClient client)
    {
        var port = GetFreeLoopbackPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        var cookieHeadersSeen = new List<string?>();
        var serverTask = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                var context = await listener.GetContextAsync();
                cookieHeadersSeen.Add(context.Request.Headers["Cookie"]);
                context.Response.Headers["Set-Cookie"] = "JSESSIONID=test-session; Path=/";
                context.Response.StatusCode = 200;
                context.Response.Close();
            }
        });

        try
        {
            (await client.GetAsync($"http://127.0.0.1:{port}/first")).Dispose();
            (await client.GetAsync($"http://127.0.0.1:{port}/second")).Dispose();
            await serverTask;
        }
        finally
        {
            listener.Stop();
        }

        return cookieHeadersSeen;
    }

    private static int GetFreeLoopbackPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
