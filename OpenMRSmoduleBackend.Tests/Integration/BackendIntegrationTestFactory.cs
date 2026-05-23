using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;

namespace OpenMRSmoduleBackend.Tests.Integration;

public sealed class BackendIntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"openmrs-backend-integration-{Guid.NewGuid():N}.db");
    private readonly Dictionary<string, string?> _previousEnvironment = [];

    private string ConnectionString => $"Data Source={_databasePath}";

    public BackendIntegrationTestFactory()
    {
        SetEnvironment("ConnectionStrings__DefaultConnection", ConnectionString);
        SetEnvironment("Database__Provider", "Sqlite");
        SetEnvironment("Database__RunMigrations", "false");
        SetEnvironment("Jwt__Issuer", "OpenMRSmoduleBackend.IntegrationTests");
        SetEnvironment("Jwt__Audience", "OpenMRSmoduleFrontend.IntegrationTests");
        SetEnvironment("Jwt__SecretKey", "integration-test-jwt-secret-32bytes");
        SetEnvironment("Security__EncryptionKey", "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");
        SetEnvironment("Webhooks__OpenMrs__Secret", OpenMrsWebhookSecret);
        SetEnvironment("Webhooks__OpenMrs__AllowedClockSkewMinutes", "5");
        SetEnvironment("Reminders__DefaultProvider", "SwiftSend");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Database:Provider"] = "Sqlite",
                ["Database:RunMigrations"] = "false",
                ["Jwt:Issuer"] = "OpenMRSmoduleBackend.IntegrationTests",
                ["Jwt:Audience"] = "OpenMRSmoduleFrontend.IntegrationTests",
                ["Jwt:SecretKey"] = "integration-test-jwt-secret-32bytes",
                ["Security:EncryptionKey"] = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=",
                ["Webhooks:OpenMrs:Secret"] = OpenMrsWebhookSecret,
                ["Webhooks:OpenMrs:AllowedClockSkewMinutes"] = "5",
                ["Reminders:DefaultProvider"] = "SwiftSend"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
        });
    }

    public const string OpenMrsWebhookSecret = "integration-webhook-secret";

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        foreach (var (key, value) in _previousEnvironment)
            Environment.SetEnvironmentVariable(key, value);

        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    private void SetEnvironment(string key, string value)
    {
        _previousEnvironment[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }
}
