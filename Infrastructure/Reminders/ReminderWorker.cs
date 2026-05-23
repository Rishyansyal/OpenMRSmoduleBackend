using Application.Messaging;
using Application.OpenMrs;
using Application.Reminders;
using Domain;
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
        var messaging = scope.ServiceProvider.GetRequiredService<IMessagingService>();
        var reminderLog = scope.ServiceProvider.GetRequiredService<IReminderLogRepository>();

        var now = DateTime.UtcNow;
        var sent = 0;

        // 24-uurs venster: encounters die starten tussen 23h en 25h vanaf nu
        var window24From = now.AddHours(23);
        var window24To = now.AddHours(25);

        // 1-uurs venster: encounters die starten tussen 55 min en 65 min vanaf nu
        var window1From = now.AddMinutes(55);
        var window1To = now.AddMinutes(65);

        var encounters24 = await openMrs.GetEncountersInRangeAsync(window24From, window24To, ct);
        var encounters1 = await openMrs.GetEncountersInRangeAsync(window1From, window1To, ct);

        sent += await ProcessWindowAsync(encounters24, "24h", openMrs, messaging, reminderLog, ct);
        sent += await ProcessWindowAsync(encounters1, "1h", openMrs, messaging, reminderLog, ct);

        if (sent > 0)
            logger.LogInformation("{count} herinneringen verstuurd.", sent);
    }

    private async Task<int> ProcessWindowAsync(
        IEnumerable<UpcomingAppointment> encounters,
        string window,
        IOpenMrsService openMrs,
        IMessagingService messaging,
        IReminderLogRepository reminderLog,
        CancellationToken ct)
    {
        var sent = 0;

        foreach (var encounter in encounters)
        {
            if (await reminderLog.AlreadySentAsync(encounter.Id, window, ct))
                continue;

            var patient = await openMrs.GetPatientAsync(encounter.PatientId, ct);
            if (patient is null)
            {
                logger.LogWarning("Patiënt {id} niet gevonden voor encounter {enc}.", encounter.PatientId, encounter.Id);
                continue;
            }

            var recipient = patient.Phone ?? patient.Email;
            if (recipient is null)
            {
                logger.LogInformation(
                    "Patiënt {name} heeft geen contactgegevens — herinnering overgeslagen.",
                    patient.DisplayName);
                continue;
            }

            var type = patient.Phone is not null ? "SMS" : "EMAIL";
            var content = BuildMessage(encounter, window);

            var result = await messaging.SendAsync(
                _options.DefaultProvider,
                new SendMessageRequest([recipient], content, type),
                ct);

            await reminderLog.LogAsync(new ReminderLog
            {
                EncounterId = encounter.Id,
                ReminderWindow = window,
                Provider = _options.DefaultProvider,
                Success = result.Success,
                ErrorCode = result.Success ? null : "SEND_ERROR",
                EncounterStart = encounter.Start
            }, ct);

            if (result.Success)
            {
                sent++;
                logger.LogInformation(
                    "Herinnering ({window}) verstuurd naar patiënt {name} voor encounter om {start}.",
                    window, patient.DisplayName, encounter.Start);
            }
            else
            {
                logger.LogWarning(
                    "Herinnering ({window}) voor {name} mislukt: {error}",
                    window, patient.DisplayName, result.Error);
            }
        }

        return sent;
    }

    private static string BuildMessage(UpcomingAppointment encounter, string window)
    {
        var timeStr = encounter.Start.ToLocalTime().ToString("dddd d MMMM 'om' HH:mm",
            new System.Globalization.CultureInfo("nl-NL"));
        var type = encounter.ServiceType ?? "afspraak";

        return window == "24h"
            ? $"Herinnering: u heeft morgen een {type} op {timeStr}. Neem contact op bij vragen."
            : $"Herinnering: u heeft over ongeveer 1 uur een {type} op {timeStr}.";
    }
}
