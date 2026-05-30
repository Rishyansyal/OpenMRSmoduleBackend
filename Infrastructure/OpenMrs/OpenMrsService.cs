using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.OpenMrs;
using Application.Organizations;

namespace Infrastructure.OpenMrs;

public class OpenMrsService(
    IHttpClientFactory httpClientFactory,
    IOrganizationConfigRepository organizationConfigs,
    ILogger<OpenMrsService> logger) : IOpenMrsService
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

    public async Task<IEnumerable<PatientContact>> SearchPatientsAsync(
        string organizationId,
        string query,
        CancellationToken ct = default)
    {
        var (client, config) = await CreateClientAsync(organizationId, ct);
        var url = $"{FhirBase(config)}/Patient?name={Uri.EscapeDataString(query)}&_count=20";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParsePatientBundle(doc.RootElement).ToList();
    }

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

    public async Task<IEnumerable<UpcomingAppointment>> GetUpcomingAppointmentsAsync(
        string organizationId,
        CancellationToken ct = default)
    {
        var (client, config) = await CreateClientAsync(organizationId, ct);
        var url = $"{FhirBase(config)}/Encounter?_count=50&_sort=-date";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseAppointmentBundle(doc.RootElement).ToList();
    }

    public async Task<IEnumerable<UpcomingAppointment>> GetEncountersInRangeAsync(
        string organizationId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var (client, config) = await CreateClientAsync(organizationId, ct);
        var f = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var t = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var url = $"{FhirBase(config)}/Encounter?date=ge{f}&date=le{t}&_count=100";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseAppointmentBundle(doc.RootElement).ToList();
    }

    public async Task<UpcomingAppointment> CreateVisitAsync(
        string organizationId,
        CreateVisitRequest request,
        CancellationToken ct = default)
    {
        var (client, config) = await CreateClientAsync(organizationId, ct);

        var startStr = request.StartUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.000+0000");
        var visit = new Dictionary<string, object?>
        {
            ["patient"] = request.PatientId,
            ["visitType"] = request.VisitTypeId,
            ["startDatetime"] = startStr
        };
        if (!string.IsNullOrWhiteSpace(request.LocationId))
            visit["location"] = request.LocationId;
        if (request.EndUtc.HasValue)
            visit["stopDatetime"] = request.EndUtc.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.000+0000");

        var json = JsonSerializer.Serialize(visit);
        var url = $"{config.OpenMrsBaseUrl.TrimEnd('/')}/openmrs/ws/rest/v1/visit";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "OpenMRS visit create failed for organization {OrganizationId}: HTTP {StatusCode}. Body: {Body}",
                organizationId,
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException("OpenMRS could not create the visit.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var visitId = root.GetProperty("uuid").GetString() ?? "";

        var fetched = await GetEncounterAsync(client, config, visitId, ct);
        if (fetched is null)
        {
            return new UpcomingAppointment(
                visitId,
                "planned",
                request.StartUtc,
                request.EndUtc,
                request.PatientId,
                "",
                request.ServiceTypeOverride,
                request.LocationOverride,
                request.Instructions);
        }

        return fetched with
        {
            ServiceType = request.ServiceTypeOverride ?? fetched.ServiceType,
            Location = request.LocationOverride ?? fetched.Location,
            Instructions = request.Instructions
        };
    }

    private async Task<UpcomingAppointment?> GetEncounterAsync(
        HttpClient client,
        OrganizationRuntimeConfig config,
        string id,
        CancellationToken ct)
    {
        var response = await client.GetAsync($"{FhirBase(config)}/Encounter/{id}", ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseAppointment(doc.RootElement);
    }

    public async Task<IEnumerable<OpenMrsReferenceItem>> GetVisitTypesAsync(
        string organizationId,
        CancellationToken ct = default) =>
        await GetReferenceListAsync(organizationId, "visittype", ct);

    public async Task<IEnumerable<OpenMrsReferenceItem>> GetLocationsAsync(
        string organizationId,
        CancellationToken ct = default) =>
        await GetReferenceListAsync(organizationId, "location", ct);

    private async Task<IEnumerable<OpenMrsReferenceItem>> GetReferenceListAsync(
        string organizationId,
        string resource,
        CancellationToken ct)
    {
        var (client, config) = await CreateClientAsync(organizationId, ct);
        var url = $"{config.OpenMrsBaseUrl.TrimEnd('/')}/openmrs/ws/rest/v1/{resource}?v=default";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("results", out var results)) return [];

        var items = new List<OpenMrsReferenceItem>();
        foreach (var r in results.EnumerateArray())
        {
            var uuid = r.TryGetProperty("uuid", out var u) ? u.GetString() : null;
            var display = r.TryGetProperty("display", out var d) ? d.GetString() : null;
            if (!string.IsNullOrEmpty(uuid) && !string.IsNullOrEmpty(display))
                items.Add(new OpenMrsReferenceItem(uuid, display));
        }
        return items;
    }

    private static IEnumerable<PatientContact> ParsePatientBundle(JsonElement bundle)
    {
        if (!bundle.TryGetProperty("entry", out var entries)) yield break;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource)) continue;
            var patient = ParsePatient(resource);
            if (patient is not null) yield return patient;
        }
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

    private static IEnumerable<UpcomingAppointment> ParseAppointmentBundle(JsonElement bundle)
    {
        if (!bundle.TryGetProperty("entry", out var entries)) yield break;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource)) continue;
            var appt = ParseAppointment(resource);
            if (appt is not null) yield return appt;
        }
    }

    private static UpcomingAppointment? ParseAppointment(JsonElement resource)
    {
        if (!resource.TryGetProperty("id", out var idEl)) return null;
        var id = idEl.GetString() ?? "";
        var status = resource.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

        DateTime start = DateTime.MinValue;
        DateTime? end = null;
        if (resource.TryGetProperty("period", out var period))
        {
            if (period.TryGetProperty("start", out var startEl))
                DateTime.TryParse(startEl.GetString(), out start);
            if (period.TryGetProperty("end", out var endEl) &&
                DateTime.TryParse(endEl.GetString(), out var endParsed))
                end = endParsed;
        }
        if (start == DateTime.MinValue) return null;

        string patientId = "", patientDisplay = "";
        if (resource.TryGetProperty("subject", out var subject))
        {
            var reference = subject.TryGetProperty("reference", out var r) ? r.GetString() ?? "" : "";
            if (reference.StartsWith("Patient/"))
                patientId = reference["Patient/".Length..];
            patientDisplay = subject.TryGetProperty("display", out var d) ? d.GetString() ?? "" : "";
        }

        string? serviceType = null;
        if (resource.TryGetProperty("type", out var types) && types.GetArrayLength() > 0)
        {
            var first = types[0];
            if (first.TryGetProperty("text", out var t))
                serviceType = t.GetString();
            else if (first.TryGetProperty("coding", out var coding) && coding.GetArrayLength() > 0)
                serviceType = coding[0].TryGetProperty("display", out var cd) ? cd.GetString() : null;
        }

        string? location = null;
        if (resource.TryGetProperty("location", out var locations) && locations.GetArrayLength() > 0)
        {
            var first = locations[0];
            if (first.TryGetProperty("location", out var loc))
            {
                if (loc.TryGetProperty("display", out var disp))
                    location = disp.GetString();
                else if (loc.TryGetProperty("reference", out var reff))
                    location = reff.GetString();
            }
        }

        return new UpcomingAppointment(id, status, start, end, patientId, patientDisplay, serviceType, location);
    }
}
