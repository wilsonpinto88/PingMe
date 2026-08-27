namespace PingMe.IntegrationTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Contracts.Catalog;
using PingMe.Api.Contracts.Locations;
using PingMe.Api.Contracts.Ordering;
using PingMe.Application.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class RealtimeOrderNotificationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public RealtimeOrderNotificationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record SeededTenant(HttpClient OwnerClient, string Token, ProductDto Product, string QrCode);

    private async Task<SeededTenant> SeedTenantWithOneOrderableProductAsync(string venueName)
    {
        var ownerClient = _factory.CreateClient();
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

        return new SeededTenant(ownerClient, auth.Token, product, qrCode!.Code);
    }

    private HubConnection BuildHubConnection(string token)
    {
        var client = _factory.CreateClient();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(client.BaseAddress!, $"/hubs/orders?access_token={token}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    [Fact]
    public async Task Placing_an_order_broadcasts_OrderReceived_to_the_owning_tenants_staff()
    {
        var seeded = await SeedTenantWithOneOrderableProductAsync("Venue A");
        await using var staffConnection = BuildHubConnection(seeded.Token);
        var received = new TaskCompletionSource<AdminOrderDto>();
        staffConnection.On<AdminOrderDto>("OrderReceived", order => received.TrySetResult(order));
        await staffConnection.StartAsync();

        var customerClient = _factory.CreateClient();
        var resolved = await (await customerClient.GetAsync($"/p/{seeded.QrCode}"))
            .Content.ReadFromJsonAsync<ResolveQrCodeResponse>();
        var orderResponse = await customerClient.PostAsJsonAsync("/orders",
            new CreateOrderRequest(resolved!.SessionId, new List<CreateOrderItemRequest> { new(seeded.Product.Id, 2) }));
        var order = await orderResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        var broadcast = await received.Task;
        Assert.Equal(order!.OrderId, broadcast.Id);
        Assert.Equal("Received", broadcast.Status);
        Assert.Single(broadcast.Items);
        Assert.Equal(2, broadcast.Items[0].Quantity);
    }
}
