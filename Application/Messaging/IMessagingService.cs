namespace Application.Messaging;

public interface IMessagingService
{
    Task<SendMessageResult> SendAsync(string providerName, SendMessageRequest request, CancellationToken ct = default);
    IEnumerable<string> GetAvailableProviders();
}
