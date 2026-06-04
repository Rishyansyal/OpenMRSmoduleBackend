using Infrastructure.Persistence;
using Infrastructure.Security;
using Application.Security;
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
        SetEnvironment("Jwt__Audience", "OpenMRSmoduleBackend.IntegrationTests");
        SetEnvironment("Jwt__SecretKey", TestJwtSecret);
        SetEnvironment("Encryption__Key", TestEncryptionKey);
        SetEnvironment("Security__EncryptionKey", TestEncryptionKey);
        SetEnvironment("Webhooks__OpenMrs__Secret", OpenMrsWebhookSecret);
        SetEnvironment("Webhooks__OpenMrs__AllowedClockSkewMinutes", "5");
        SetEnvironment("Reminders__DefaultProvider", "SwiftSend");
        SetEnvironment("Admin__AllowPublicRegistration", "true");
        SetEnvironment("Admin__SeedOnStartup", "false");
        SetEnvironment("HospitalConfiguration__SeedOnStartup", "false");
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
                ["Jwt:Audience"] = "OpenMRSmoduleBackend.IntegrationTests",
                ["Jwt:SecretKey"] = TestJwtSecret,
                ["Encryption:Key"] = TestEncryptionKey,
                ["Security:EncryptionKey"] = TestEncryptionKey,
                ["Webhooks:OpenMrs:Secret"] = OpenMrsWebhookSecret,
                ["Webhooks:OpenMrs:AllowedClockSkewMinutes"] = "5",
                ["Reminders:DefaultProvider"] = "SwiftSend",
                ["Admin:AllowPublicRegistration"] = "true",
                ["Admin:SeedOnStartup"] = "false",
                ["HospitalConfiguration:SeedOnStartup"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.Configure<EncryptionOptions>(options =>
            {
                options.Key = TestEncryptionKey;
            });
            services.TryAddScoped<IEncryptionService, AesEncryptionService>();
        });
    }

    public static readonly string OpenMrsWebhookSecret = string.Concat("integration", "-webhook", "-signing", "-key");
    private static readonly string TestJwtSecret = string.Concat("integration", "-jwt", "-signing", "-key", "-32bytes");
    private static readonly string TestEncryptionKey = Convert.ToBase64String(
        Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await SeedOrganizationAsync(scope.ServiceProvider, db);
    }

    private static async Task SeedOrganizationAsync(IServiceProvider services, ApplicationDbContext db)
    {
        var encryption = services.GetRequiredService<IFieldEncryptionService>();
        db.OrganizationIntegrationConfigs.Add(new Domain.OrganizationIntegrationConfig
        {
            OrganizationId = "lu1",
            OpenMrsBaseUrl = "http://openmrs.test",
            OpenMrsUsernameEncrypted = encryption.Encrypt("openmrs-user"),
            OpenMrsPasswordEncrypted = encryption.Encrypt("openmrs-password"),
            WebhookSecretEncrypted = encryption.Encrypt(OpenMrsWebhookSecret),
            Enabled = true,
            DefaultProvider = "SwiftSend",
            TimeZoneId = "UTC",
            MaxDeliveryAttempts = 10,
            RetryBaseDelaySeconds = 60,
            RetryMaxDelayMinutes = 60
        });
        await db.SaveChangesAsync();
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
