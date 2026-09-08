namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.Api.Contracts.Venues;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class VenueBrandingTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public VenueBrandingTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<HttpClient> RegisterAndAuthenticateAsync(PingMeWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("The Rooftop", email, "P@ssw0rd123"));
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private static UpdateVenueBrandingRequest ValidBranding() => new(
        "#0f766e",
        "#facc15",
        "gbp",
        "Dark",
        "https://cdn.example.com/logo.png",
        "https://cdn.example.com/hero.jpg",
        "Cocktails with a view");

    [Fact]
    public async Task A_newly_registered_tenant_has_a_venue_with_default_branding()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);

        var response = await client.GetAsync("/admin/venues");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var venues = await response.Content.ReadFromJsonAsync<List<VenueDto>>();
        var venue = Assert.Single(venues!);
        Assert.Equal("The Rooftop", venue.Name);
        Assert.Equal("#111827", venue.Theme.PrimaryColor);
        Assert.Equal("EUR", venue.Theme.CurrencyCode);
        Assert.Equal("Light", venue.Theme.ThemeMode);
    }

    [Fact]
    public async Task Owner_can_update_venue_branding()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);
        var venues = await client.GetFromJsonAsync<List<VenueDto>>("/admin/venues");
        var venueId = venues![0].Id;

        var response = await client.PutAsJsonAsync($"/admin/venues/{venueId}/branding", ValidBranding());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<VenueDto>();
        Assert.Equal("#0F766E", updated!.Theme.PrimaryColor);
        Assert.Equal("GBP", updated.Theme.CurrencyCode);
        Assert.Equal("Dark", updated.Theme.ThemeMode);
        Assert.Equal("Cocktails with a view", updated.Theme.Tagline);
    }

    [Fact]
    public async Task Scanning_a_qr_code_returns_that_venues_branding()
    {
        var client = await RegisterAndAuthenticateAsync(_factory);
        var venues = await client.GetFromJsonAsync<List<VenueDto>>("/admin/venues");
        await client.PutAsJsonAsync($"/admin/venues/{venues![0].Id}/branding", ValidBranding());

        var locationResponse = await client.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 4", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();
        var qrResponse = await client.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));
        var qrCode = await qrResponse.Content.ReadFromJsonAsync<QrCodeDto>();

        var anonymous = _factory.CreateClient();
        var resolved = await anonymous.GetFromJsonAsync<ResolveQrCodeResponse>($"/p/{qrCode!.Code}");

        Assert.Equal("The Rooftop", resolved!.VenueName);
        Assert.Equal("Table 4", resolved.LocationLabel);
        Assert.Equal("#0F766E", resolved.Theme.PrimaryColor);
        Assert.Equal("#FACC15", resolved.Theme.AccentColor);
        Assert.Equal("GBP", resolved.Theme.CurrencyCode);
        Assert.Equal("Dark", resolved.Theme.ThemeMode);
        Assert.Equal("https://cdn.example.com/logo.png", resolved.Theme.LogoUrl);
        Assert.Equal("Cocktails with a view", resolved.Theme.Tagline);
    }

    [Theory]
    [InlineData("not-a-color", "#FACC15", "EUR", "Light", null)]
    [InlineData("#0F766E", "#FACC15", "EURO", "Light", null)]
    [InlineData("#0F766E", "#FACC15", "EUR", "Neon", null)]
    [InlineData("#0F766E", "#FACC15", "EUR", "1", null)]
    [InlineData("#0F766E", "#FACC15", "EUR", "Light", "javascript:alert(1)")]
    public async Task Invalid_branding_is_rejected_with_400(
        string primary, string accent, string currency, string themeMode, string? logoUrl)
    {
        var client = await RegisterAndAuthenticateAsync(_factory);
        var venues = await client.GetFromJsonAsync<List<VenueDto>>("/admin/venues");

        var response = await client.PutAsJsonAsync(
            $"/admin/venues/{venues![0].Id}/branding",
            new UpdateVenueBrandingRequest(primary, accent, currency, themeMode, logoUrl, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task One_tenant_cannot_rebrand_another_tenants_venue()
    {
        var tenantA = await RegisterAndAuthenticateAsync(_factory);
        var tenantB = await RegisterAndAuthenticateAsync(_factory);
        var venuesA = await tenantA.GetFromJsonAsync<List<VenueDto>>("/admin/venues");

        var response = await tenantB.PutAsJsonAsync($"/admin/venues/{venuesA![0].Id}/branding", ValidBranding());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
