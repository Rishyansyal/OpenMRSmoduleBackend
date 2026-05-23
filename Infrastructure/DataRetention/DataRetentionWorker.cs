using Application.DataRetention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.DataRetention;

public class DataRetentionWorker(
    IServiceProvider serviceProvider,
    IOptions<DataRetentionOptions> options,
    ILogger<DataRetentionWorker> logger) : BackgroundService
{
    private readonly DataRetentionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("DataRetentionWorker is uitgeschakeld via configuratie.");
            return;
        }

        logger.LogInformation(
            "DataRetentionWorker gestart — patiëntdata: {p} dagen, meta-logs: {m} dagen.",
            _options.PatientDataRetentionDays, _options.MessageLogRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fout tijdens data-retentie cleanup.");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    public async Task<DataRetentionResult> ProcessAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
        var result = await service.RunAsync(ct);

        logger.LogInformation(
            "Data-retentie voltooid: {r} reminder-logs, {m} bericht-logs, {a} afspraken, {w} webhook-events verwijderd.",
            result.ReminderLogsDeleted,
            result.MessageLogsDeleted,
            result.AppointmentNotificationsDeleted,
            result.WebhookEventLogsDeleted);

        return result;
    }
}
