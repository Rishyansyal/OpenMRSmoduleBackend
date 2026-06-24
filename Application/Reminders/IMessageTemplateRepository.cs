using Domain;

namespace Application.Reminders;

public interface IMessageTemplateRepository
{
    Task<List<MessageTemplate>> GetAllAsync(CancellationToken ct = default);
    Task<MessageTemplate?> GetByWindowAsync(string window, CancellationToken ct = default);
}
