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

    public async Task<IEnumerable<MessageLog>> GetRecentByUserAsync(string userId, int count = 50, CancellationToken ct = default) =>
        await db.MessageLogs
            .Where(m => m.SentByUserId == userId)
            .OrderByDescending(m => m.SentAt)
            .Take(count)
            .ToListAsync(ct);

    public Task<bool> UserOwnsProviderMessageIdAsync(string userId, string providerMessageId, CancellationToken ct = default) =>
        db.MessageLogs.AnyAsync(
            m => m.SentByUserId == userId && m.ProviderMessageId == providerMessageId,
            ct);
}
