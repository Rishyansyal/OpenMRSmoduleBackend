namespace Application.Messaging;

public interface IAsyncMessageProvider : IMessageProvider
{
    Task<MessageStatusResult> GetStatusAsync(
        string trackingId,
        MessageProviderConfiguration? configuration = null,
        CancellationToken ct = default);
}
