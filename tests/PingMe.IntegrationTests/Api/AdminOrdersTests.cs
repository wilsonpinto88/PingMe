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

public class AdminOrdersTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public AdminOrdersTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededOrder(HttpClient OwnerClient, Guid OrderId);

    private static async Task<SeededOrder> SeedOneOrderAsync(PingMeWebApplicationFactory factory)
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
            $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Product", 5.00m));
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductDto>())!;
        var locationResponse = await ownerClient.PostAsJsonAsync("/admin/locations", new CreateLocationRequest("Table 1", null));
        var location = await locationResponse.Content.ReadFromJsonAsync<LocationDto>();
        var qrResponse = await ownerClient.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id));
        var qrCode = await qrResponse.Content.ReadFromJsonAsync<QrCodeDto>();

        var customerClient = factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{qrCode!.Code}")).Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var order = await (await customerClient.PostAsJsonAsync("/orders",
                new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(product.Id, 1) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();

        return new SeededOrder(ownerClient, order!.OrderId);
    }

    [Fact]
    public async Task Owner_can_list_orders_and_see_the_new_order()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.GetAsync("/admin/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<AdminOrderDto>>();
        Assert.Contains(orders!, o => o.Id == seeded.OrderId && o.Status == "Received");
    }

    [Fact]
    public async Task Owner_can_transition_an_order_to_Accepted()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{seeded.OrderId}/status", new UpdateOrderStatusRequest("Accepted"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Skipping_a_status_transition_returns_409()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{seeded.OrderId}/status", new UpdateOrderStatusRequest("Preparing"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_status_string_returns_400()
    {
        var seeded = await SeedOneOrderAsync(_factory);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{seeded.OrderId}/status", new UpdateOrderStatusRequest("NotARealStatus"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
