using Application.Security;
using Domain;
using Infrastructure.Configuration;
using Infrastructure.Organizations;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace OpenMRSmoduleBackend.Tests.Organizations;

public class OrganizationConfigSeederTests
{
    [Fact]
    public async Task SeedAsync_DisablesIncompleteLegacyOrganization()
    {
        await using var fixture = await DbFixture.CreateAsync();
        fixture.Db.OrganizationIntegrationConfigs.Add(new OrganizationIntegrationConfig
        {
            OrganizationId = "legacy",
            OpenMrsBaseUrl = "",
            OpenMrsUsernameEncrypted = "",
            OpenMrsPasswordEncrypted = "",
            WebhookSecretEncrypted = "",
            Enabled = true,
            PollerEnabled = true
        });
        await fixture.Db.SaveChangesAsync();

        await fixture.Seeder.SeedAsync();

        var organization = await fixture.Db.OrganizationIntegrationConfigs.SingleAsync();
        Assert.False(organization.Enabled);
        Assert.False(organization.PollerEnabled);
    }

    [Fact]
    public async Task SeedAsync_RepairsConfiguredLegacyOrganizationWithVersionedCiphertext()
    {
        await using var fixture = await DbFixture.CreateAsync(
            new HospitalOrganizationOptions
            {
                OrganizationId = "legacy",
                OpenMrsBaseUrl = "http://openmrs",
                OpenMrsUsername = "service-account",
                OpenMrsPassword = "secret-password",
                WebhookSecret = "webhook-secret",
                Enabled = true
            });
        fixture.Db.OrganizationIntegrationConfigs.Add(new OrganizationIntegrationConfig
        {
            OrganizationId = "legacy",
            OpenMrsBaseUrl = "",
            OpenMrsUsernameEncrypted = "",
            OpenMrsPasswordEncrypted = "",
            WebhookSecretEncrypted = "",
            Enabled = true
        });
        await fixture.Db.SaveChangesAsync();

        await fixture.Seeder.SeedAsync();

        var organization = await fixture.Db.OrganizationIntegrationConfigs.SingleAsync();
        Assert.True(organization.Enabled);
        Assert.StartsWith("v1:", organization.OpenMrsUsernameEncrypted);
        Assert.StartsWith("v1:", organization.OpenMrsPasswordEncrypted);
        Assert.StartsWith("v1:", organization.WebhookSecretEncrypted);
    }

    private sealed class DbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public ApplicationDbContext Db { get; }
        public OrganizationConfigSeeder Seeder { get; }

        public static async Task<DbFixture> CreateAsync(params HospitalOrganizationOptions[] organizations)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:EncryptionKey"] = TestEncryptionKey
                })
                .Build();
            var encryptionOptions = Options.Create(new EncryptionOptions { Key = TestEncryptionKey });
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new ApplicationDbContext(dbOptions, new AesEncryptionService(encryptionOptions));
            await db.Database.EnsureCreatedAsync();

            var seeder = new OrganizationConfigSeeder(
                db,
                new FieldEncryptionService(configuration),
                configuration,
                Options.Create(new HospitalConfigurationOptions { Organizations = [.. organizations] }),
                NullLogger<OrganizationConfigSeeder>.Instance);

            return new DbFixture(connection, db, seeder);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private DbFixture(
            SqliteConnection connection,
            ApplicationDbContext db,
            OrganizationConfigSeeder seeder)
        {
            _connection = connection;
            Db = db;
            Seeder = seeder;
        }
    }

    private static readonly string TestEncryptionKey = Convert.ToBase64String(
        Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
}
