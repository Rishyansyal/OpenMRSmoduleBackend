using System.Net;
using System.Text.Json;

namespace OpenMRSmoduleBackend.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class SwaggerContractTests(BackendIntegrationTestFactory factory)
{
    [Fact]
    public async Task SwaggerJson_ContainsBearerSecurityDefinition()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var bearer = json.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");

        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
    }
}

