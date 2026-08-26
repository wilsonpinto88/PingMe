namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class AuthTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthTests(PingMeWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterTenant_returns_201_with_a_token()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task RegisterTenant_with_duplicate_email_returns_409()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        var secondAttempt = await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Another Venue", email, "DifferentPass1"));

        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task Login_after_register_succeeds()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        var loginResponse = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "P@ssw0rd123"));

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Test Venue", email, "P@ssw0rd123"));

        var loginResponse = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "WrongPassword1"));

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401_not_404()
    {
        var response = await _client.PostAsJsonAsync("/auth/login",
            new LoginRequest($"unknown-{Guid.NewGuid():N}@example.com", "whatever123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
