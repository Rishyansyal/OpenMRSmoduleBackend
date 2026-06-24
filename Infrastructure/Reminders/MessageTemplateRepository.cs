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


}
