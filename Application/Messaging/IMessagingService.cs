namespace Application.Messaging;

public interface IMessagingService
{
    Task<SendMessageResult> SendAsync(
        string providerName,
        SendMessageRequest request,
        string? organizationId = null,
        CancellationToken ct = default);

    IEnumerable<string> GetAvailableProviders();
}
