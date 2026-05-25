using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.OpenMrs;
using Microsoft.Extensions.Options;

namespace Infrastructure.OpenMrs;

public class OpenMrsService(IHttpClientFactory httpClientFactory, IOptions<OpenMrsOptions> options) : IOpenMrsService
{
    private readonly OpenMrsOptions _options = options.Value;

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        return client;
    }

    public async Task<IEnumerable<PatientContact>> SearchPatientsAsync(string query, CancellationToken ct = default)
    {
        var client = CreateClient();
        var url = $"{_options.FhirBase}/Patient?name={Uri.EscapeDataString(query)}&_count=20";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParsePatientBundle(doc.RootElement).ToList();
    }

    public async Task<PatientContact?> GetPatientAsync(string patientId, CancellationToken ct = default)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"{_options.FhirBase}/Patient/{patientId}", ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParsePatient(doc.RootElement);
    }

    public async Task<IEnumerable<UpcomingAppointment>> GetUpcomingAppointmentsAsync(CancellationToken ct = default)
    {
        var client = CreateClient();
        var url = $"{_options.FhirBase}/Encounter?_count=50&_sort=-date";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseAppointmentBundle(doc.RootElement).ToList();
    }

    public async Task<IEnumerable<UpcomingAppointment>> GetEncountersInRangeAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        var client = CreateClient();
        var f = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var t = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var url = $"{_options.FhirBase}/Encounter?date=ge{f}&date=le{t}&_count=100";
        var response = await client.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseAppointmentBundle(doc.RootElement).ToList();
    }

    public async Task<UpcomingAppointment> CreateVisitAsync(CreateVisitRequest request, CancellationToken ct = default)
    {
        var client = CreateClient();

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
        var url = $"{_options.BaseUrl.TrimEnd('/')}/openmrs/ws/rest/v1/visit";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"OpenMRS POST /visit faalde ({(int)response.StatusCode}): {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var visitId = root.GetProperty("uuid").GetString() ?? "";

        // FHIR2 mapping is meestal direct beschikbaar; haal Encounter op om
        // de canonieke representatie (status, service-type, location-display) terug te krijgen.
        var fetched = await GetEncounterAsync(visitId, ct);
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

    private async Task<UpcomingAppointment?> GetEncounterAsync(string id, CancellationToken ct)
    {
        var client = CreateClient();
        var response = await client.GetAsync($"{_options.FhirBase}/Encounter/{id}", ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return ParseAppointment(doc.RootElement);
    }

    public async Task<IEnumerable<OpenMrsReferenceItem>> GetVisitTypesAsync(CancellationToken ct = default) =>
        await GetReferenceListAsync("visittype", ct);

    public async Task<IEnumerable<OpenMrsReferenceItem>> GetLocationsAsync(CancellationToken ct = default) =>
        await GetReferenceListAsync("location", ct);

    private async Task<IEnumerable<OpenMrsReferenceItem>> GetReferenceListAsync(string resource, CancellationToken ct)
    {
        var client = CreateClient();
        var url = $"{_options.BaseUrl.TrimEnd('/')}/openmrs/ws/rest/v1/{resource}?v=default";
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

    // --- FHIR parsers ---

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

                // OpenMRS FHIR2 retourneert telecom soms zonder system — heuristisch raden.
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

        // Encounter gebruikt period.start i.p.v. start
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

        // Encounter gebruikt subject i.p.v. participant[].actor
        string patientId = "", patientDisplay = "";
        if (resource.TryGetProperty("subject", out var subject))
        {
            var reference = subject.TryGetProperty("reference", out var r) ? r.GetString() ?? "" : "";
            if (reference.StartsWith("Patient/"))
                patientId = reference["Patient/".Length..];
            patientDisplay = subject.TryGetProperty("display", out var d) ? d.GetString() ?? "" : "";
        }

        // Encounter type → type[0].coding[0].display of type[0].text
        string? serviceType = null;
        if (resource.TryGetProperty("type", out var types) && types.GetArrayLength() > 0)
        {
            var first = types[0];
            if (first.TryGetProperty("text", out var t))
                serviceType = t.GetString();
            else if (first.TryGetProperty("coding", out var coding) && coding.GetArrayLength() > 0)
                serviceType = coding[0].TryGetProperty("display", out var cd) ? cd.GetString() : null;
        }

        // Encounter.location[0].location.display
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
