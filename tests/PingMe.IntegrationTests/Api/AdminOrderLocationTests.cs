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

/// <summary>
/// Staff cannot deliver an order they cannot locate, so every order surfaced to
/// the dashboard must carry the label of the location its QR code belongs to.
/// </summary>
public class AdminOrderLocationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public AdminOrderLocationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record Seeded(HttpClient OwnerClient, Guid ProductId, string QrCode);

    private static async Task<Seeded> SeedAsync(PingMeWebApplicationFactory factory, string locationName)
    {
        var owner = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        await owner.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest("Venue", email, "P@ssw0rd123"));
        var login = await owner.PostAsJsonAsync("/auth/login", new LoginRequest(email, "P@ssw0rd123"));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        var menu = await (await owner.PostAsJsonAsync("/admin/menus", new CreateMenuRequest("Menu")))
            .Content.ReadFromJsonAsync<MenuDto>();
        var category = await (await owner.PostAsJsonAsync(
                $"/admin/menus/{menu!.Id}/categories", new CreateCategoryRequest("Category", 1)))
            .Content.ReadFromJsonAsync<CategoryDto>();
        var product = await (await owner.PostAsJsonAsync(
                $"/admin/products/categories/{category!.Id}", new CreateProductRequest("Burger", 9.50m)))
            .Content.ReadFromJsonAsync<ProductDto>();

        var location = await (await owner.PostAsJsonAsync(
                "/admin/locations", new CreateLocationRequest(locationName, null)))
            .Content.ReadFromJsonAsync<LocationDto>();
        var qr = await (await owner.PostAsJsonAsync("/admin/qrcodes", new CreateQrCodeRequest(location!.Id)))
            .Content.ReadFromJsonAsync<QrCodeDto>();

        return new Seeded(owner, product!.Id, qr!.Code);
    }

    private static async Task<Guid> PlaceOrderAsync(PingMeWebApplicationFactory factory, Seeded seeded)
    {
        var customer = factory.CreateClient();
        var resolved = await customer.GetFromJsonAsync<ResolveQrCodeResponse>($"/p/{seeded.QrCode}");
        var created = await (await customer.PostAsJsonAsync("/orders",
                new CreateOrderRequest(
                    resolved!.SessionId,
                    new List<CreateOrderItemRequest> { new(seeded.ProductId, 2) })))
            .Content.ReadFromJsonAsync<CreateOrderResponse>();
        return created!.OrderId;
    }

    [Fact]
    public async Task An_order_in_the_dashboard_carries_the_location_it_was_placed_from()
    {
        var seeded = await SeedAsync(_factory, "Balcony Table 7");
        await PlaceOrderAsync(_factory, seeded);

        var orders = await seeded.OwnerClient.GetFromJsonAsync<List<AdminOrderDto>>("/admin/orders");

        var order = Assert.Single(orders!);
        Assert.Equal("Balcony Table 7", order.LocationLabel);
    }

    [Fact]
    public async Task Advancing_an_order_keeps_its_location_on_the_broadcast_payload()
    {
        var seeded = await SeedAsync(_factory, "Row 12 Seat 4");
        var orderId = await PlaceOrderAsync(_factory, seeded);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{orderId}/status", new UpdateOrderStatusRequest("Accepted"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var orders = await seeded.OwnerClient.GetFromJsonAsync<List<AdminOrderDto>>("/admin/orders");
        var order = Assert.Single(orders!);
        Assert.Equal("Accepted", order.Status);
        Assert.Equal("Row 12 Seat 4", order.LocationLabel);
    }

    [Fact]
    public async Task A_numeric_order_status_is_rejected_rather_than_parsed_as_an_enum_value()
    {
        var seeded = await SeedAsync(_factory, "Table 1");
        var orderId = await PlaceOrderAsync(_factory, seeded);

        var response = await seeded.OwnerClient.PutAsJsonAsync(
            $"/admin/orders/{orderId}/status", new UpdateOrderStatusRequest("1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
