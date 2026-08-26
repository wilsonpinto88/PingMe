namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class CatalogAdminIsolationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public CatalogAdminIsolationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client)
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));

        var loginResponse = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest(email, "P@ssw0rd123"));
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private static async Task<ProductDto> CreateProductForTenantAsync(HttpClient client)
    {
        var menuResponse = await client.PostAsJsonAsync("/admin/menus", new CreateMenuRequest("Menu"));
        var menu = await menuResponse.Content.ReadFromJsonAsync<MenuDto>();

        var categoryResponse = await client.PostAsJsonAsync(
            $"/admin/menus/{menu!.Id}/categories", new CreateCategoryRequest("Category", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();

        var productResponse = await client.PostAsJsonAsync(
            $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Product", 9.99m));
        return (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    [Fact]
    public async Task TenantA_cannot_read_TenantB_product_via_admin_api()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientA));
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientB));

        var productB = await CreateProductForTenantAsync(clientB);

        var response = await clientA.GetAsync($"/admin/products/{productB.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_cannot_modify_TenantB_product_availability()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientA));
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientB));

        var productB = await CreateProductForTenantAsync(clientB);

        var response = await clientA.PutAsJsonAsync(
            $"/admin/products/{productB.Id}/availability", new UpdateProductAvailabilityRequest(false));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_cannot_add_option_to_TenantB_product()
    {
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientA));
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await RegisterAndLoginAsync(clientB));

        var productB = await CreateProductForTenantAsync(clientB);

        var response = await clientA.PostAsJsonAsync(
            $"/admin/products/{productB.Id}/options", new CreateProductOptionRequest("Extra cheese", 1.50m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_to_admin_endpoint_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/admin/menus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
