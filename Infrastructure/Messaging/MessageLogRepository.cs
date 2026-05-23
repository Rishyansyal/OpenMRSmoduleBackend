using Application.Messaging;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Messaging;

public class MessageLogRepository(ApplicationDbContext db) : IMessageLogRepository
{
    public async Task LogAsync(MessageLog entry, CancellationToken ct = default)
    {
        db.MessageLogs.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<MessageLog>> GetRecentAsync(int count = 50, CancellationToken ct = default) =>
        await db.MessageLogs
            .OrderByDescending(m => m.SentAt)
            .Take(count)
            .ToListAsync(ct);
}
