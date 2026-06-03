using Application.Messaging.Commands;
using Application.Reminders;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Reminders;

public class ReminderWorker(
    IServiceProvider serviceProvider,
    IOptions<ReminderOptions> options,
    ILogger<ReminderWorker> logger) : BackgroundService
{
    private readonly ReminderOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("ReminderWorker is uitgeschakeld via configuratie.");
            return;
        }

        logger.LogInformation("ReminderWorker gestart — interval: {interval} min.", _options.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fout tijdens verwerking herinneringen.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
        }
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var scheduledReminders = scope.ServiceProvider.GetRequiredService<IScheduledReminderRepository>();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var now = DateTime.UtcNow;
        var published = 0;
        var due = await scheduledReminders.ClaimDueAsync(now, _options.BatchSize, ct);

        foreach (var reminder in due)
        {
            var messageId = NewId.NextGuid();
            await scheduledReminders.RecordQueuePublishAsync(reminder.ScheduledReminderId, messageId, ct);

            try
            {
                await bus.Publish(new SendReminderCommand(
                    reminder.ScheduledReminderId,
                    reminder.OrganizationId,
                    reminder.EncounterId,
                    reminder.PatientId,
                    reminder.ReminderWindow,
                    reminder.EncounterStart,
                    reminder.ServiceType,
                    reminder.Provider,
                    reminder.AttemptCount,
                    reminder.MaxAttempts,
                    reminder.Location,
                    reminder.Instructions), context => context.MessageId = messageId, ct);

                published++;
                logger.LogDebug("SendReminderCommand gepubliceerd: reminder {id}, venster {window}, queue message {messageId}.",
                    reminder.ScheduledReminderId, reminder.ReminderWindow, messageId);
            }
            catch (Exception ex)
            {
                await scheduledReminders.RecordQueuePublishFailureAsync(
                    reminder.ScheduledReminderId,
                    "QUEUE_PUBLISH_FAILED",
                    ct);
                logger.LogError(ex, "Kon reminder {id} niet naar RabbitMQ publiceren.",
                    reminder.ScheduledReminderId);
            }
        }

        if (published > 0)
            logger.LogInformation("{count} herinnering(en) in de queue geplaatst.", published);
    }
}
