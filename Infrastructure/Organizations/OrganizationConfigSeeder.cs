using System.Text.Json;
using Application.Security;
using Domain;
using Infrastructure.Configuration;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Organizations;

public class OrganizationConfigSeeder(
    ApplicationDbContext db,
    IFieldEncryptionService encryption,
    IConfiguration configuration,
    IOptions<HospitalConfigurationOptions> options)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var organizations = options.Value.Organizations.Count > 0
            ? options.Value.Organizations
            : BuildLegacyOrganizationOptions();

        foreach (var organization in organizations.Where(o => !string.IsNullOrWhiteSpace(o.OrganizationId)))
        {
            await UpsertOrganizationAsync(organization, ct);
            foreach (var provider in organization.Providers.Where(p => !string.IsNullOrWhiteSpace(p.ProviderName)))
                await UpsertProviderAsync(organization.OrganizationId, provider, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task UpsertOrganizationAsync(HospitalOrganizationOptions source, CancellationToken ct)
    {
        var organization = await db.OrganizationIntegrationConfigs
            .SingleOrDefaultAsync(o => o.OrganizationId == source.OrganizationId, ct);

        if (organization is null)
        {
            organization = new OrganizationIntegrationConfig
            {
                OrganizationId = source.OrganizationId
            };
            db.OrganizationIntegrationConfigs.Add(organization);
        }

        organization.OpenMrsBaseUrl = source.OpenMrsBaseUrl.TrimEnd('/');
        organization.OpenMrsUsernameEncrypted = encryption.Encrypt(source.OpenMrsUsername);
        organization.OpenMrsPasswordEncrypted = encryption.Encrypt(source.OpenMrsPassword);
        organization.WebhookSecretEncrypted = encryption.Encrypt(source.WebhookSecret);
        organization.Enabled = source.Enabled;
        organization.DefaultProvider = source.DefaultProvider;
        organization.TimeZoneId = string.IsNullOrWhiteSpace(source.TimeZoneId) ? "UTC" : source.TimeZoneId;
        organization.PollerEnabled = source.PollerEnabled;
        organization.PollerIntervalMinutes = Math.Max(1, source.PollerIntervalMinutes);
        organization.PollerLookaheadHours = Math.Max(1, source.PollerLookaheadHours);
        organization.MaxDeliveryAttempts = Math.Max(1, source.MaxDeliveryAttempts);
        organization.RetryBaseDelaySeconds = Math.Max(10, source.RetryBaseDelaySeconds);
        organization.RetryMaxDelayMinutes = Math.Max(1, source.RetryMaxDelayMinutes);
        organization.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task UpsertProviderAsync(
        string organizationId,
        HospitalProviderOptions source,
        CancellationToken ct)
    {
        var providerName = source.ProviderName.ToLowerInvariant();
        var provider = await db.OrganizationProviderConfigs
            .SingleOrDefaultAsync(
                p => p.OrganizationId == organizationId && p.ProviderName == providerName,
                ct);

        if (provider is null)
        {
            provider = new OrganizationProviderConfig
            {
                OrganizationId = organizationId,
                ProviderName = providerName
            };
            db.OrganizationProviderConfigs.Add(provider);
        }

        provider.Enabled = source.Enabled;
        provider.BaseUrl = source.BaseUrl.TrimEnd('/');
        provider.StudentGroup = source.StudentGroup;
        provider.CredentialsJsonEncrypted = encryption.Encrypt(JsonSerializer.Serialize(source.Credentials));
        provider.UpdatedAtUtc = DateTime.UtcNow;
    }

    private List<HospitalOrganizationOptions> BuildLegacyOrganizationOptions()
    {
        var organizationId =
            configuration["OpenMrs:Poller:OrganizationId"] ??
            configuration["OpenMrs:OrganizationId"] ??
            "openmrs-local";

        var openMrsBaseUrl = configuration["OpenMrs:BaseUrl"] ?? "";
        var openMrsUsername = configuration["OpenMrs:Username"] ?? "";
        var openMrsPassword = configuration["OpenMrs:Password"] ?? "";
        var webhookSecret = configuration["Webhooks:OpenMrs:Secret"] ?? "";

        if (string.IsNullOrWhiteSpace(openMrsBaseUrl) ||
            string.IsNullOrWhiteSpace(openMrsUsername) ||
            string.IsNullOrWhiteSpace(openMrsPassword) ||
            string.IsNullOrWhiteSpace(webhookSecret))
        {
            return [];
        }

        var studentGroup = configuration["Messaging:StudentGroup"] ?? "";
        var providerBaseUrl = configuration["Messaging:SwiftSend:BaseUrl"] ?? "";

        return
        [
            new HospitalOrganizationOptions
            {
                OrganizationId = organizationId,
                OpenMrsBaseUrl = openMrsBaseUrl,
                OpenMrsUsername = openMrsUsername,
                OpenMrsPassword = openMrsPassword,
                WebhookSecret = webhookSecret,
                Enabled = true,
                DefaultProvider = configuration["Reminders:DefaultProvider"] ?? "swiftsend",
                PollerEnabled = configuration.GetValue("OpenMrs:Poller:Enabled", false),
                PollerIntervalMinutes = configuration.GetValue("OpenMrs:Poller:IntervalMinutes", 5),
                PollerLookaheadHours = configuration.GetValue("OpenMrs:Poller:LookaheadHours", 48),
                Providers =
                [
                    Provider("swiftsend", configuration["Messaging:SwiftSend:BaseUrl"] ?? providerBaseUrl, studentGroup,
                        new Dictionary<string, string> { ["apiKey"] = configuration["Messaging:SwiftSend:ApiKey"] ?? "" }),
                    Provider("securepost", configuration["Messaging:SecurePost:BaseUrl"] ?? providerBaseUrl, studentGroup,
                        new Dictionary<string, string>
                        {
                            ["clientId"] = configuration["Messaging:SecurePost:ClientId"] ?? "",
                            ["clientSecret"] = configuration["Messaging:SecurePost:ClientSecret"] ?? ""
                        }),
                    Provider("legacylink", configuration["Messaging:LegacyLink:BaseUrl"] ?? providerBaseUrl, studentGroup,
                        new Dictionary<string, string>
                        {
                            ["username"] = configuration["Messaging:LegacyLink:Username"] ?? "",
                            ["password"] = configuration["Messaging:LegacyLink:Password"] ?? ""
                        }),
                    Provider("asyncflow", configuration["Messaging:AsyncFlow:BaseUrl"] ?? providerBaseUrl, studentGroup,
                        new Dictionary<string, string> { ["apiKey"] = configuration["Messaging:AsyncFlow:ApiKey"] ?? "" })
                ]
            }
        ];
    }

    private static HospitalProviderOptions Provider(
        string name,
        string baseUrl,
        string studentGroup,
        Dictionary<string, string> credentials) =>
        new()
        {
            ProviderName = name,
            Enabled = true,
            BaseUrl = baseUrl,
            StudentGroup = studentGroup,
            Credentials = credentials
        };
}
