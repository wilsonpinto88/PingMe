namespace PingMe.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class OrderingIsolationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public OrderingIsolationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededTenant(HttpClient OwnerClient, ProductDto Product, string QrCode);

    private static async Task<SeededTenant> SeedTenantWithOneOrderableProductAsync(PingMeWebApplicationFactory factory, string venueName)
    {
        var ownerClient = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await ownerClient.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest(venueName, email, "P@ssw0rd123"));
        var loginResponse = await ownerClient.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var menuResponse = await ownerClient.PostAsJsonAsync("/admin/menus", new CreateMenuRequest("Menu"));
        var menu = await menuResponse.Content.ReadFromJsonAsync<MenuDto>();
        var categoryResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/menus/{menu!.Id}/categories", new CreateCategoryRequest("Category", 1));
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
        var productResponse = await ownerClient.PostAsJsonAsync(
            $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Product", 5.00m));
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;

        var locationResponse = await ownerClient.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 1", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();
        var qrResponse = await ownerClient.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));
        var qrCode = await qrResponse.Content.ReadFromJsonAsync<QrCodeDto>();

        return new SeededTenant(ownerClient, product, qrCode!.Code);
    }

    [Fact]
    public async Task CustomerSession_from_TenantA_submitting_TenantB_productId_on_orders_is_rejected()
    {
        var tenantA = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue A");
        var tenantB = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue B");

        var customerClient = _factory.CreateClient();
        var resolvedA = await (await customerClient.GetAsync($"/p/{tenantA.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var response = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolvedA!.SessionId, new List<CreateOrderItemRequest> { new(tenantB.Product.Id, 1) }));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Polling_order_status_with_a_session_from_a_different_tenant_returns_404()
    {
        var tenantA = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue A");
        var tenantB = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue B");

        var customerClient = _factory.CreateClient();
        var resolvedA = await (await customerClient.GetAsync($"/p/{tenantA.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var orderA = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolvedA!.SessionId, new List<CreateOrderItemRequest> { new(tenantA.Product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        var resolvedB = await (await customerClient.GetAsync($"/p/{tenantB.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();

        var response = await customerClient.GetAsync($"/orders/{orderA!.OrderId}/status?sessionId={resolvedB!.SessionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TenantA_owner_cannot_see_TenantB_order_via_admin_orders_endpoint()
    {
        var tenantA = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue A");
        var tenantB = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue B");

        var customerClient = _factory.CreateClient();
        var resolvedB = await (await customerClient.GetAsync($"/p/{tenantB.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var orderB = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolvedB!.SessionId, new List<CreateOrderItemRequest> { new(tenantB.Product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        var response = await tenantA.OwnerClient.GetAsync("/admin/orders");
        var orders = await response.Content.ReadFromJsonAsync<List<AdminOrderDto>>();

        Assert.DoesNotContain(orders!, o => o.Id == orderB!.OrderId);
    }

    [Fact]
    public async Task TenantA_owner_cannot_transition_TenantB_order_status()
    {
        var tenantA = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue A");
        var tenantB = await SeedTenantWithOneOrderableProductAsync(_factory, "Venue B");

        var customerClient = _factory.CreateClient();
        var resolvedB = await (await customerClient.GetAsync($"/p/{tenantB.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var orderB = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolvedB!.SessionId, new List<CreateOrderItemRequest> { new(tenantB.Product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        var response = await tenantA.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{orderB!.OrderId}/status", new UpdateOrderStatusRequest("Accepted"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
