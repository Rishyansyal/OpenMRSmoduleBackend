using Application.Messaging;
using Application.Organizations;

namespace Infrastructure.Messaging;

public class MessagingService(
    IEnumerable<IMessageProvider> providers,
    IOrganizationConfigRepository organizationConfigRepository) : IMessagingService
{
    private readonly Dictionary<string, IMessageProvider> _providers =
        providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);

    public async Task<SendMessageResult> SendAsync(
        string providerName,
        SendMessageRequest request,
        string? organizationId = null,
        CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
            throw new ArgumentException($"Unknown provider: '{providerName}'");

        MessageProviderConfiguration? providerConfig = null;
        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            providerConfig = await organizationConfigRepository.GetProviderAsync(organizationId, providerName, ct);
            if (providerConfig is null)
                throw new InvalidOperationException(
                    $"Provider '{providerName}' is not configured for organization '{organizationId}'.");
        }

        return await provider.SendAsync(request, providerConfig, ct);
    }

    public IEnumerable<string> GetAvailableProviders() => _providers.Keys;
}
