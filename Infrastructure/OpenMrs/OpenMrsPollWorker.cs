using System.Security.Cryptography;
using System.Text;
using Application.OpenMrs;
using Application.Organizations;
using Application.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.OpenMrs;

public class OpenMrsPollWorker(
    IServiceProvider serviceProvider,
    IOptions<OpenMrsPollerOptions> options,
    ILogger<OpenMrsPollWorker> logger) : BackgroundService
{
    private readonly OpenMrsPollerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var startupScope = serviceProvider.CreateScope();
        var startupConfigs = startupScope.ServiceProvider.GetRequiredService<IOrganizationConfigRepository>();
        var configuredOrganizations = await startupConfigs.GetPollingEnabledAsync(stoppingToken);
        if (!_options.Enabled && configuredOrganizations.Count == 0)
        {
            logger.LogInformation("OpenMrsPollWorker is uitgeschakeld via configuratie.");
            return;
        }

        logger.LogInformation(
            "OpenMrsPollWorker gestart — interval: {interval} min, lookahead: {hours} u, organisatie: {org}.",
            _options.IntervalMinutes, _options.LookaheadHours, _options.OrganizationId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fout tijdens OpenMRS-poll.");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken);
        }
    }

    public async Task PollAsync(CancellationToken ct)
    {
        using var configScope = serviceProvider.CreateScope();
        var organizationConfigs = configScope.ServiceProvider.GetRequiredService<IOrganizationConfigRepository>();
        var organizations = await organizationConfigs.GetPollingEnabledAsync(ct);
        foreach (var organization in organizations)
        {
            using var scope = serviceProvider.CreateScope();
            var openMrs = scope.ServiceProvider.GetRequiredService<IOpenMrsService>();
            var webhookService = scope.ServiceProvider.GetRequiredService<IOpenMrsWebhookService>();

            var now = DateTime.UtcNow;
            var to = now.AddHours(organization.PollerLookaheadHours);

            var encounters = await openMrs.GetEncountersInRangeAsync(organization.OrganizationId, now, to, ct);
            var seen = 0;
            var accepted = 0;
            var duplicates = 0;

            foreach (var enc in encounters)
            {
                if (string.IsNullOrWhiteSpace(enc.PatientId) || enc.Start == DateTime.MinValue)
                    continue;

                seen++;

                var payload = new OpenMrsAppointmentWebhookRequest(
                    EncounterId: enc.Id,
                    PatientId: enc.PatientId,
                    Start: enc.Start,
                    Status: string.IsNullOrWhiteSpace(enc.Status) ? "planned" : enc.Status,
                    End: enc.End,
                    PatientDisplay: string.IsNullOrWhiteSpace(enc.PatientDisplay) ? null : enc.PatientDisplay,
                    ServiceType: enc.ServiceType,
                    Location: enc.Location,
                    Instructions: enc.Instructions);

                var eventId = BuildSyntheticEventId(payload);

                try
                {
                    var result = await webhookService.ProcessAppointmentAsync(
                        eventId,
                        eventType: "POLLED",
                        organizationId: organization.OrganizationId,
                        eventTimestamp: DateTimeOffset.UtcNow,
                        payload: payload,
                        ct);

                    if (result.Duplicate) duplicates++;
                    else if (result.Accepted) accepted++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Poll-event voor encounter {enc} kon niet worden verwerkt.", enc.Id);
                }
            }

            if (seen > 0)
                logger.LogInformation(
                    "OpenMRS-poll voor {org}: {seen} encounters gezien, {accepted} nieuw/gewijzigd, {dup} ongewijzigd.",
                    organization.OrganizationId, seen, accepted, duplicates);
        }
    }

    private static string BuildSyntheticEventId(OpenMrsAppointmentWebhookRequest payload)
    {
        var canonical = string.Join("|",
            payload.EncounterId,
            payload.PatientId,
            payload.Start.ToUniversalTime().ToString("O"),
            payload.End?.ToUniversalTime().ToString("O") ?? "",
            payload.Status,
            payload.ServiceType ?? "",
            payload.Location ?? "",
            payload.Instructions ?? "");

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
        return $"poll-{payload.EncounterId}-{hash[..16]}";
    }
}
