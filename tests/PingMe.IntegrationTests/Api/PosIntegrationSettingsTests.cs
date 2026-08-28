namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Integrations;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class PosIntegrationSettingsTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public PosIntegrationSettingsTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> RegisterOwnerAsync(string venueName)
    {
        var client = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest(venueName, email, "P@ssw0rd123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Fact]
    public async Task Unauthenticated_request_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/pos-integration");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Getting_settings_before_any_are_configured_returns_204()
    {
        var owner = await RegisterOwnerAsync("Venue A");

        var response = await owner.GetAsync("/admin/pos-integration");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Upserting_settings_then_getting_them_returns_the_saved_values()
    {
        var owner = await RegisterOwnerAsync("Venue B");

        var putResponse = await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/webhook", true));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var getResponse = await owner.GetAsync("/admin/pos-integration");
        var settings = await getResponse.Content.ReadFromJsonAsync<PosIntegrationSettingsDto>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("Webhook", settings!.ProviderType);
        Assert.Equal("http://example.invalid/webhook", settings.WebhookUrl);
        Assert.True(settings.IsEnabled);
    }

    [Fact]
    public async Task Upserting_twice_updates_the_same_row_instead_of_creating_a_second_one()
    {
        var owner = await RegisterOwnerAsync("Venue C");

        await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/first", true));
        await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/second", false));

        var getResponse = await owner.GetAsync("/admin/pos-integration");
        var settings = await getResponse.Content.ReadFromJsonAsync<PosIntegrationSettingsDto>();

        Assert.Equal("http://example.invalid/second", settings!.WebhookUrl);
        Assert.False(settings.IsEnabled);
    }

    [Fact]
    public async Task Upserting_with_an_invalid_provider_type_returns_400()
    {
        var owner = await RegisterOwnerAsync("Venue D");

        var response = await owner.PutAsJsonAsync("/admin/pos-integration",
            new UpsertPosIntegrationSettingsRequest("NotARealProvider", "http://example.invalid/webhook", true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_cannot_see_TenantBs_pos_integration_settings()
    {
        var ownerA = await RegisterOwnerAsync("Venue E");
        var ownerB = await RegisterOwnerAsync("Venue F");

        await owner_B_configures(ownerB);

        var response = await ownerA.GetAsync("/admin/pos-integration");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        async Task owner_B_configures(HttpClient client)
        {
            await client.PutAsJsonAsync("/admin/pos-integration",
                new UpsertPosIntegrationSettingsRequest("Webhook", "http://example.invalid/tenant-b", true));
        }
    }
}
