using Application.Messaging;

namespace Infrastructure.Messaging;

public class MessagingService(IEnumerable<IMessageProvider> providers) : IMessagingService
{
    private readonly Dictionary<string, IMessageProvider> _providers =
        providers.ToDictionary(p => p.ProviderName, StringComparer.OrdinalIgnoreCase);

    public async Task<SendMessageResult> SendAsync(string providerName, SendMessageRequest request, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out var provider))
            throw new ArgumentException($"Unknown provider: '{providerName}'");

        return await provider.SendAsync(request, ct);
    }

    public IEnumerable<string> GetAvailableProviders() => _providers.Keys;
}
