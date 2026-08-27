namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class OrdersHubAuthTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public OrdersHubAuthTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Negotiating_the_orders_hub_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiating_the_orders_hub_with_a_valid_token_returns_200()
    {
        var client = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var response = await client.PostAsync(
            $"/hubs/orders/negotiate?negotiateVersion=1&access_token={auth!.Token}", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
