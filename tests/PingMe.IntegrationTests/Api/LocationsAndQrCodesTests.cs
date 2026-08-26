namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Locations;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class LocationsAndQrCodesTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public LocationsAndQrCodesTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<HttpClient> RegisterAndAuthenticateAsync(PingMeWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    [Fact]
    public async Task Owner_can_create_a_location_after_registering()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);

        var response = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 12", null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = await response.Content.ReadFromJsonAsync<LocationDto>();
        Assert.Equal("Table 12", location!.Name);
        Assert.Null(location.ParentLocationId);
    }

    [Fact]
    public async Task Owner_can_create_a_nested_location()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);
        var parentResponse = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Section B", null));
        var parent = await parentResponse.Content.ReadFromJsonAsync<LocationDto>();

        var childResponse = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Row 12", parent!.Id));

        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var child = await childResponse.Content.ReadFromJsonAsync<LocationDto>();
        Assert.Equal(parent.Id, child!.ParentLocationId);
    }

    [Fact]
    public async Task Creating_a_location_with_unknown_parent_returns_404()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);

        var response = await client.PostAsJsonAsync(
            "/admin/locations", new CreateLocationRequest("Row 12", Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Owner_can_create_a_qr_code_for_a_location()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);
        var locationResponse = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 1", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();

        var response = await client.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var qrCode = await response.Content.ReadFromJsonAsync<QrCodeDto>();
        Assert.Equal(location.Id, qrCode!.LocationId);
        Assert.Equal(8, qrCode.Code.Length);
    }

    [Fact]
    public async Task Creating_a_qr_code_for_unknown_location_returns_404()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);

        var response = await client.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
