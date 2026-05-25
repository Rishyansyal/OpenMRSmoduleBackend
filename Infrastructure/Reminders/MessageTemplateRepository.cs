using Application.Reminders;
using Domain;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Reminders;

public class MessageTemplateRepository(ApplicationDbContext db) : IMessageTemplateRepository
{
    public Task<List<MessageTemplate>> GetAllAsync(CancellationToken ct = default) =>
        db.MessageTemplates.OrderBy(t => t.Window).ToListAsync(ct);

    public Task<MessageTemplate?> GetByWindowAsync(string window, CancellationToken ct = default) =>
        db.MessageTemplates.FirstOrDefaultAsync(t => t.Window == window, ct);

    public async Task UpsertAsync(MessageTemplate template, CancellationToken ct = default)
    {
        var existing = await db.MessageTemplates.FindAsync([template.Window], ct);
        if (existing is null)
        {
            db.MessageTemplates.Add(template);
        }
        else
        {
            existing.Body = template.Body;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
