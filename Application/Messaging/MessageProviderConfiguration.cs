namespace Application.Messaging;

public record MessageProviderConfiguration(
    string OrganizationId,
    string ProviderName,
    string BaseUrl,
    string StudentGroup,
    IReadOnlyDictionary<string, string> Credentials)
{
    public string? GetCredential(string key) =>
        Credentials.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
}

