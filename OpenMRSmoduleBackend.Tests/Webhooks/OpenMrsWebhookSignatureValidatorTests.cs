using Application.Organizations;
using Infrastructure.Webhooks;
using Microsoft.Extensions.Options;

namespace OpenMRSmoduleBackend.Tests.Webhooks;

public class OpenMrsWebhookSignatureValidatorTests
{
    [Fact]
    public async Task Validate_AcceptsValidSignature()
    {
        var secret = TestSigningKey;
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, secret);
        var validator = CreateValidator(secret);

        var result = await validator.ValidateAsync("org-1", timestamp, signature, body);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_RejectsReplayOutsideClockSkew()
    {
        var secret = TestSigningKey;
        const string body = """{"encounterId":"enc-1"}""";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("O");
        var signature = "sha256=" + OpenMrsWebhookSignatureValidator.ComputeSignatureHex(timestamp, body, secret);
        var validator = CreateValidator(secret);

        var result = await validator.ValidateAsync("org-1", timestamp, signature, body);

        Assert.False(result.IsValid);
        Assert.Equal("STALE_TIMESTAMP", result.ErrorCode);
    }

    [Fact]
    public async Task Validate_RejectsInvalidSignature()
    {
        var validator = CreateValidator(TestSigningKey);

        var result = await validator.ValidateAsync("org-1", DateTimeOffset.UtcNow.ToString("O"), "sha256=deadbeef", "{}");

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_SIGNATURE", result.ErrorCode);
    }

    private static OpenMrsWebhookSignatureValidator CreateValidator(string secret) =>
        new(Options.Create(new OpenMrsWebhookOptions
        {
            Secret = secret,
            AllowedClockSkewMinutes = 5
        }), new FakeOrganizationConfigRepository(secret));

    private sealed class FakeOrganizationConfigRepository(string secret) : IOrganizationConfigRepository
    {
        private readonly OrganizationRuntimeConfig _config = new(
            "org-1", "https://openmrs.test", "user", "password", secret, "swiftsend", "UTC",
            true, false, 5, 48, 10, 60, 60);

        public Task<OrganizationRuntimeConfig?> GetByIdAsync(string organizationId, CancellationToken ct = default) =>
            Task.FromResult<OrganizationRuntimeConfig?>(organizationId == "org-1" ? _config : null);

        public Task<OrganizationRuntimeConfig?> GetDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult<OrganizationRuntimeConfig?>(_config);

        public Task<IReadOnlyList<OrganizationRuntimeConfig>> GetPollingEnabledAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OrganizationRuntimeConfig>>([]);

        public Task<Application.Messaging.MessageProviderConfiguration?> GetProviderAsync(
            string organizationId,
            string providerName,
            CancellationToken ct = default) =>
            Task.FromResult<Application.Messaging.MessageProviderConfiguration?>(null);
    }

    private static readonly string TestSigningKey = string.Concat("test", "-webhook", "-signing", "-key");
}
