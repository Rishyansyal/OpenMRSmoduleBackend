using Application.Messaging;

namespace Application.Organizations;

public interface IOrganizationConfigRepository
{
    Task<OrganizationRuntimeConfig?> GetByIdAsync(string organizationId, CancellationToken ct = default);
    Task<OrganizationRuntimeConfig?> GetDefaultAsync(CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationRuntimeConfig>> GetPollingEnabledAsync(CancellationToken ct = default);
    Task<MessageProviderConfiguration?> GetProviderAsync(
        string organizationId,
        string providerName,
        CancellationToken ct = default);
}

