using Application.Messaging.Commands;
using Application.OpenMrs;
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
        var openMrs = scope.ServiceProvider.GetRequiredService<IOpenMrsService>();
        var reminderLog = scope.ServiceProvider.GetRequiredService<IReminderLogRepository>();
        var bus = scope.ServiceProvider.GetRequiredService<IBus>();

        var now = DateTime.UtcNow;
        var published = 0;

        var windows = new[]
        {
            ("24h", now.AddHours(23), now.AddHours(25)),
            ("1h",  now.AddMinutes(55), now.AddMinutes(65))
        };

        foreach (var (window, from, to) in windows)
        {
            var encounters = await openMrs.GetEncountersInRangeAsync(from, to, ct);

            foreach (var encounter in encounters)
            {
                // Controleer duplicaat vóór publicatie (ook consumer is idempotent als fallback)
                if (await reminderLog.AlreadySentAsync(encounter.Id, window, ct))
                    continue;

                await bus.Publish(new SendReminderCommand(
                    encounter.Id,
                    encounter.PatientId,
                    window,
                    encounter.Start,
                    encounter.ServiceType,
                    _options.DefaultProvider), ct);

                published++;
                logger.LogDebug("SendReminderCommand gepubliceerd: encounter {id}, venster {window}.",
                    encounter.Id, window);
            }
        }

        if (published > 0)
            logger.LogInformation("{count} herinnering(en) in de queue geplaatst.", published);
    }
}
