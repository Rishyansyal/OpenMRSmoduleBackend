namespace Application.Messaging;

public interface IAsyncMessageProvider : IMessageProvider
{
    Task<MessageStatusResult> GetStatusAsync(string trackingId, CancellationToken ct = default);
}
