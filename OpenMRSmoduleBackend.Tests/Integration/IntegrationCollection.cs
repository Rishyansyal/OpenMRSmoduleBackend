namespace OpenMRSmoduleBackend.Tests.Integration;

/// <summary>
/// Integration-tests delen één <see cref="BackendIntegrationTestFactory"/> en
/// draaien serieel. Reden: de factory zet proces-brede environment variables
/// (connection string, JWT-secret) die elkaar overschrijven als test-klassen parallel
/// instantiëren.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<BackendIntegrationTestFactory>
{
    public const string Name = "Integration";
}
