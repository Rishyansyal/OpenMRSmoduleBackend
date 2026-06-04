namespace Application.Messaging;

public interface IMessageProvider
{
    string ProviderName { get; }
    Task<SendMessageResult> SendAsync(
        SendMessageRequest request,
        MessageProviderConfiguration? configuration = null,
        CancellationToken ct = default);
}
