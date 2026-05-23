using Application.DataRetention;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.DataRetention;

public class DataRetentionService(
    ApplicationDbContext db,
    IOptions<DataRetentionOptions> options) : IDataRetentionService
{
    private readonly DataRetentionOptions _options = options.Value;

    public async Task<DataRetentionResult> RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Verwijder reminder_logs waarvan de afspraakdatum ouder is dan 14 dagen
        var reminderCutoff = now.AddDays(-_options.PatientDataRetentionDays);
        var reminderDeleted = await db.ReminderLogs
            .Where(r => r.EncounterStart < reminderCutoff)
            .ExecuteDeleteAsync(ct);

        // Verwijder message_logs ouder dan 1 jaar
        var messageCutoff = now.AddDays(-_options.MessageLogRetentionDays);
        var messageDeleted = await db.MessageLogs
            .Where(m => m.SentAt < messageCutoff)
            .ExecuteDeleteAsync(ct);

        return new DataRetentionResult(reminderDeleted, messageDeleted);
    }
}
