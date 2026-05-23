namespace Infrastructure.OpenMrs;

public class OpenMrsOptions
{
    public string BaseUrl { get; set; } = "http://localhost:3032";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FhirBase => $"{BaseUrl.TrimEnd('/')}/openmrs/ws/fhir2/R4";
}
