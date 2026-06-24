using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.OpenMrs;
using Application.Organizations;

namespace Infrastructure.OpenMrs;

public class OpenMrsService(
    IHttpClientFactory httpClientFactory,
    IOrganizationConfigRepository organizationConfigs) : IOpenMrsService
{
    private async Task<(HttpClient Client, OrganizationRuntimeConfig Config)> CreateClientAsync(
        string organizationId,
        CancellationToken ct)
    {
        var config = await organizationConfigs.GetByIdAsync(organizationId, ct)
            ?? throw new InvalidOperationException("OpenMRS organization is not configured or is disabled.");

        var client = httpClientFactory.CreateClient("openmrs");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{config.OpenMrsUsername}:{config.OpenMrsPassword}"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        return (client, config);
    }

    private static string FhirBase(OrganizationRuntimeConfig config) =>
        $"{config.OpenMrsBaseUrl.TrimEnd('/')}/openmrs/ws/fhir2/R4";

    public async Task<PatientContact?> GetPatientAsync(
        string organizationId,
        string patientId,
        CancellationToken ct = default)
    {
        var (client, config) = await CreateClientAsync(organizationId, ct);
        var response = await client.GetAsync($"{FhirBase(config)}/Patient/{patientId}", ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParsePatient(doc.RootElement);
    }

    // Haalt afspraken uit de Bahmni Appointment Scheduling-module (de O3 "Appointments"-app).
    // Dit is een ander resourcetype dan FHIR Encounters: afspraken worden vooruit gepland en
    // vormen de basis voor afspraakherinneringen.
    public async Task<IEnumerable<UpcomingAppointment>> GetAppointmentsInRangeAsync(
        string organizationId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var (client, config) = await CreateClientAsync(organizationId, ct);
        var requestBody = JsonSerializer.Serialize(new
        {
            startDate = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            endDate = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        });
        var url = $"{config.OpenMrsBaseUrl.TrimEnd('/')}/openmrs/ws/rest/v1/appointments/search";
        using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseBahmniAppointments(doc.RootElement).ToList();
    }

    private static PatientContact? ParsePatient(JsonElement resource)
    {
        if (!resource.TryGetProperty("id", out var idEl)) return null;
        var id = idEl.GetString() ?? "";

        var displayName = "";
        if (resource.TryGetProperty("name", out var names) && names.GetArrayLength() > 0)
        {
            var nameEl = names[0];
            if (nameEl.TryGetProperty("text", out var text))
                displayName = text.GetString() ?? "";
            else
            {
                var given = nameEl.TryGetProperty("given", out var g)
                    ? string.Join(" ", g.EnumerateArray().Select(x => x.GetString()))
                    : "";
                var family = nameEl.TryGetProperty("family", out var f) ? f.GetString() : "";
                displayName = $"{given} {family}".Trim();
            }
        }

        string? phone = null, email = null;
        if (resource.TryGetProperty("telecom", out var telecoms))
        {
            foreach (var t in telecoms.EnumerateArray())
            {
                var system = t.TryGetProperty("system", out var s) ? s.GetString() : null;
                var value = t.TryGetProperty("value", out var v) ? v.GetString() : null;
                if (string.IsNullOrWhiteSpace(value)) continue;

                if (string.IsNullOrEmpty(system))
                    system = value.Contains('@') ? "email" : "phone";

                if (system == "phone" && phone is null) phone = value;
                if (system == "email" && email is null) email = value;
            }
        }

        return new PatientContact(id, displayName, phone, email);
    }

    private static IEnumerable<UpcomingAppointment> ParseBahmniAppointments(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) yield break;
        foreach (var element in root.EnumerateArray())
        {
            var appointment = ParseBahmniAppointment(element);
            if (appointment is not null) yield return appointment;
        }
    }

    private static UpcomingAppointment? ParseBahmniAppointment(JsonElement a)
    {
        var id = a.TryGetProperty("uuid", out var u) ? u.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(id)) return null;

        if (!a.TryGetProperty("startDateTime", out var startEl) || startEl.ValueKind != JsonValueKind.Number)
            return null;
        var start = DateTimeOffset.FromUnixTimeMilliseconds(startEl.GetInt64()).UtcDateTime;

        DateTime? end = null;
        if (a.TryGetProperty("endDateTime", out var endEl) && endEl.ValueKind == JsonValueKind.Number)
            end = DateTimeOffset.FromUnixTimeMilliseconds(endEl.GetInt64()).UtcDateTime;

        var status = a.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        string patientId = "", patientDisplay = "";
        if (a.TryGetProperty("patient", out var patient) && patient.ValueKind == JsonValueKind.Object)
        {
            patientId = patient.TryGetProperty("uuid", out var pu) ? pu.GetString() ?? "" : "";
            patientDisplay = patient.TryGetProperty("name", out var pn) ? pn.GetString() ?? "" : "";
        }

        string? serviceType = null;
        if (a.TryGetProperty("service", out var service) && service.ValueKind == JsonValueKind.Object &&
            service.TryGetProperty("name", out var sn))
            serviceType = sn.GetString();

        string? location = null;
        if (a.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object &&
            loc.TryGetProperty("name", out var ln))
            location = ln.GetString();

        var instructions = a.TryGetProperty("comments", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(instructions)) instructions = null;

        return new UpcomingAppointment(id, status, start, end, patientId, patientDisplay, serviceType, location, instructions);
    }
}
