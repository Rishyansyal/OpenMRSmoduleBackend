using Domain;

namespace Application.Messaging;

public interface IMessageLogRepository
{
    Task LogAsync(MessageLog entry, CancellationToken ct = default);
    Task<IEnumerable<MessageLog>> GetRecentAsync(int count = 50, CancellationToken ct = default);
}
