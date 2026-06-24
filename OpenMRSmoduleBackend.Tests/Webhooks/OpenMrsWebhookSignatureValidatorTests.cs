using Application.Organizations;
using Infrastructure.Webhooks;
using Microsoft.Extensions.Options;

namespace OpenMRSmoduleBackend.Tests.Webhooks;

public class OpenMrsWebhookSignatureValidatorTests
{
    [Fact]
    public async Task Validate_AcceptsValidSignature()
    {
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, Org1Secret);
        var validator = CreateValidator();

        var result = await validator.ValidateAsync("org-1", timestamp, signature, body);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_RejectsReplayOutsideClockSkew()
    {
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("O");
        var signature = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, Org1Secret);
        var validator = CreateValidator();

        var result = await validator.ValidateAsync("org-1", timestamp, signature, body);

        Assert.False(result.IsValid);
        Assert.Equal("STALE_TIMESTAMP", result.ErrorCode);
    }

    [Fact]
    public async Task Validate_RejectsInvalidSignature()
    {
        var validator = CreateValidator();

        var result = await validator.ValidateAsync("org-1", DateTimeOffset.UtcNow.ToString("O"), "sha256=deadbeef", "{}");

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public async Task Validate_RejectsUnknownOrganization()
    {
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, Org1Secret);
        var validator = CreateValidator();

        var result = await validator.ValidateAsync("org-does-not-exist", timestamp, signature, body);

        Assert.False(result.IsValid);
        Assert.Equal("UNKNOWN_ORGANIZATION", result.ErrorCode);
    }

    // Multi-tenant isolatie: een payload die met het secret van organisatie 1 is ondertekend,
    // maar die zich voordoet als organisatie 2, moet worden afgewezen — anders zou organisatie A
    // webhooks namens organisatie B kunnen vervalsen.
    [Fact]
    public async Task Validate_RejectsSignatureSignedWithAnotherOrganizationsSecret()
    {
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signatureFromOrg1 = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, Org1Secret);
        var validator = CreateValidator();

        // Claimt org-2, maar is ondertekend met het secret van org-1.
        var result = await validator.ValidateAsync("org-2", timestamp, signatureFromOrg1, body);

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    [Fact]
    public async Task Validate_AcceptsEachOrganizationWithItsOwnSecret()
    {
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var validator = CreateValidator();

        var org1 = await validator.ValidateAsync(
            "org-1", timestamp,
            "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, Org1Secret), body);
        var org2 = await validator.ValidateAsync(
            "org-2", timestamp,
            "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, Org2Secret), body);

        Assert.True(org1.IsValid);
        Assert.True(org2.IsValid);
    }

    private static OpenMrsWebhookSignatureValidator CreateValidator() =>
        new(Options.Create(new OpenMrsWebhookOptions
        {
            Secret = "fallback-secret-not-used",
            AllowedClockSkewMinutes = 5
        }), new FakeOrganizationConfigRepository(new Dictionary<string, string>
        {
            ["org-1"] = Org1Secret,
            ["org-2"] = Org2Secret
        }));

    private sealed class FakeOrganizationConfigRepository(IReadOnlyDictionary<string, string> secretsByOrg)
        : IOrganizationConfigRepository
    {
        private static OrganizationRuntimeConfig Build(string orgId, string secret) => new(
            orgId, "https://openmrs.test", "user", "password", secret, "swiftsend", "UTC",
            true, false, 5, 48, 10, 60, 60);

        public Task<OrganizationRuntimeConfig?> GetByIdAsync(string organizationId, CancellationToken ct = default) =>
            Task.FromResult(secretsByOrg.TryGetValue(organizationId, out var secret)
                ? Build(organizationId, secret)
                : null);

        public Task<OrganizationRuntimeConfig?> GetDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult<OrganizationRuntimeConfig?>(Build("org-1", secretsByOrg["org-1"]));

        public Task<IReadOnlyList<OrganizationRuntimeConfig>> GetPollingEnabledAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OrganizationRuntimeConfig>>([]);

        public Task<Application.Messaging.MessageProviderConfiguration?> GetProviderAsync(
            string organizationId,
            string providerName,
            CancellationToken ct = default) =>
            Task.FromResult<Application.Messaging.MessageProviderConfiguration?>(null);
    }

    private static readonly string Org1Secret = string.Concat("org1", "-webhook", "-secret");
    private static readonly string Org2Secret = string.Concat("org2", "-webhook", "-secret");
}
