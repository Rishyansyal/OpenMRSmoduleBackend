using System.Text.Json;
using Application.Messaging;
using Application.Organizations;
using Application.Security;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Organizations;

public class OrganizationConfigRepository(
    ApplicationDbContext db,
    IFieldEncryptionService encryption) : IOrganizationConfigRepository
{
    public async Task<OrganizationRuntimeConfig?> GetByIdAsync(
        string organizationId,
        CancellationToken ct = default)
    {
        var config = await db.OrganizationIntegrationConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                o => o.OrganizationId == organizationId && o.Enabled,
                ct);

        return config is null ? null : ToRuntime(config);
    }

    public async Task<OrganizationRuntimeConfig?> GetDefaultAsync(CancellationToken ct = default)
    {
        var config = await db.OrganizationIntegrationConfigs
            .AsNoTracking()
            .Where(o => o.Enabled)
            .OrderBy(o => o.OrganizationId)
            .FirstOrDefaultAsync(ct);

        return config is null ? null : ToRuntime(config);
    }

    public async Task<IReadOnlyList<OrganizationRuntimeConfig>> GetPollingEnabledAsync(CancellationToken ct = default)
    {
        var configs = await db.OrganizationIntegrationConfigs
            .AsNoTracking()
            .Where(o => o.Enabled && o.PollerEnabled)
            .OrderBy(o => o.OrganizationId)
            .ToListAsync(ct);

        return configs.Select(ToRuntime).ToList();
    }

    public async Task<MessageProviderConfiguration?> GetProviderAsync(
        string organizationId,
        string providerName,
        CancellationToken ct = default)
    {
        var provider = await db.OrganizationProviderConfigs
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.OrganizationId == organizationId &&
                     p.ProviderName.ToLower() == providerName.ToLower() &&
                     p.Enabled,
                ct);

        if (provider is null)
            return null;

        var credentialsJson = encryption.Decrypt(provider.CredentialsJsonEncrypted);
        var credentials = JsonSerializer.Deserialize<Dictionary<string, string>>(credentialsJson)
                          ?? [];

        return new MessageProviderConfiguration(
            provider.OrganizationId,
            provider.ProviderName,
            provider.BaseUrl,
            provider.StudentGroup,
            credentials);
    }

    private OrganizationRuntimeConfig ToRuntime(Domain.OrganizationIntegrationConfig config) =>
        new(
            config.OrganizationId,
            config.OpenMrsBaseUrl,
            encryption.Decrypt(config.OpenMrsUsernameEncrypted),
            encryption.Decrypt(config.OpenMrsPasswordEncrypted),
            encryption.Decrypt(config.WebhookSecretEncrypted),
            config.DefaultProvider,
            config.TimeZoneId,
            config.Enabled,
            config.PollerEnabled,
            config.PollerIntervalMinutes,
            config.PollerLookaheadHours,
            config.MaxDeliveryAttempts,
            config.RetryBaseDelaySeconds,
            config.RetryMaxDelayMinutes);
}
