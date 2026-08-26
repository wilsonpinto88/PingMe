namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class CustomerOrderingFlowTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public CustomerOrderingFlowTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededTenant(HttpClient OwnerClient, ProductDto Product, string QrCode);

    private static async Task<SeededTenant> SeedTenantWithOneOrderableProductAsync(PingMeWebApplicationFactory factory)
    {
        var ownerClient = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await ownerClient.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
        var loginResponse = await ownerClient.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var menuResponse = await ownerClient.PostAsJsonAsync("/admin/menus", new CreateMenuRequest("Menu"));
        var menu = await menuResponse.Content.ReadFromJsonAsync<MenuDto>();
        var categoryResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/menus/{menu!.Id}/categories", new CreateCategoryRequest("Category", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        var productResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Burger", 9.50m));
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;

        var locationResponse = await ownerClient.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 1", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();
        var qrResponse = await ownerClient.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));
        var qrCode = await qrResponse.Content.ReadFromJsonAsync<QrCodeDto>();

        return new SeededTenant(ownerClient, product, qrCode!.Code);
    }

    [Fact]
    public async Task Scanning_a_qr_code_returns_the_venue_menu()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();

        var response = await customerClient.GetAsync($"/p/{seeded.QrCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        Assert.NotEqual(Guid.Empty, body!.SessionId);
        Assert.Contains(body.Menus, m => m.Categories.Any(c => c.Products.Any(p => p.Id == seeded.Product.Id)));
    }

    [Fact]
    public async Task Scanning_the_same_code_twice_resumes_the_same_session()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();

        var first = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var second = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        Assert.Equal(first!.SessionId, second!.SessionId);
    }

    [Fact]
    public async Task Unknown_qr_code_returns_404()
    {
        var customerClient = _factory.CreateClient();

        var response = await customerClient.GetAsync("/p/DOESNOTEXIST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_can_place_an_order_and_poll_its_status()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 2) }));

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        var order = await orderResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.Equal("Received", order!.Status);

        var statusResponse = await customerClient.GetAsync($"/orders/{order.OrderId}/status?sessionId={resolved.SessionId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<OrderStatusResponse>();
        Assert.Equal("Received", status!.Status);
    }

    [Fact]
    public async Task Placing_an_order_with_unavailable_product_returns_409()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        await seeded.OwnerClient.PutAsJsonAsync($"/admin/products/{seeded.Product.Id}/availability",
            new UpdateProductAvailabilityRequest(false));
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) }));

        Assert.Equal(HttpStatusCode.Conflict, orderResponse.StatusCode);
    }

    [Fact]
    public async Task Placing_an_order_against_a_closed_session_returns_409()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync(_factory);
        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var locationsResponse = await seeded.OwnerClient.GetAsync("/admin/locations");
        var locations = await locationsResponse.Content.ReadFromJsonAsync<List<LocationDto>>();
        await seeded.OwnerClient.PostAsync($"/admin/locations/{locations![0].Id}/close-session", null);

        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 1) }));

        Assert.Equal(HttpStatusCode.Conflict, orderResponse.StatusCode);
    }
}
