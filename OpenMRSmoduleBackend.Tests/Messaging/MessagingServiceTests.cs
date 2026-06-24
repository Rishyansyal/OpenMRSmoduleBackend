using Application.Messaging;
using Application.Organizations;
using Infrastructure.Messaging;

namespace OpenMRSmoduleBackend.Tests.Messaging;

public class MessagingServiceTests
{
    [Fact]
    public async Task SendAsync_WhenOrganizationProviderMissing_DoesNotFallbackToAnotherProvider()
    {
        var swiftSend = new RecordingProvider("swiftsend");
        var service = new MessagingService(
            [swiftSend, new RecordingProvider("securepost")],
            new MissingProviderOrganizationRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(
                "securepost",
                new SendMessageRequest(["+31600000000"], "synthetic message", "SMS"),
                "hospital-a"));

        Assert.False(swiftSend.WasCalled);
    }

    private sealed class RecordingProvider(string providerName) : IMessageProvider
    {
        public string ProviderName { get; } = providerName;
        public bool WasCalled { get; private set; }

        public Task<SendMessageResult> SendAsync(
            SendMessageRequest request,
            MessageProviderConfiguration? configuration = null,
            CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(new SendMessageResult(true, "provider-id", null, []));
        }
    }

    private sealed class MissingProviderOrganizationRepository : IOrganizationConfigRepository
    {
        public Task<OrganizationRuntimeConfig?> GetByIdAsync(string organizationId, CancellationToken ct = default) =>
            Task.FromResult<OrganizationRuntimeConfig?>(null);

        public Task<OrganizationRuntimeConfig?> GetDefaultAsync(CancellationToken ct = default) =>
            Task.FromResult<OrganizationRuntimeConfig?>(null);

        public Task<IReadOnlyList<OrganizationRuntimeConfig>> GetPollingEnabledAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OrganizationRuntimeConfig>>([]);

        public Task<MessageProviderConfiguration?> GetProviderAsync(
            string organizationId,
            string providerName,
            CancellationToken ct = default) =>
            Task.FromResult<MessageProviderConfiguration?>(null);
    }
}
