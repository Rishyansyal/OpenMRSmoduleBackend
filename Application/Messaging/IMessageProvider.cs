namespace Application.Messaging;

public interface IMessageProvider
{
    string ProviderName { get; }
    Task<SendMessageResult> SendAsync(SendMessageRequest request, CancellationToken ct = default);
}
