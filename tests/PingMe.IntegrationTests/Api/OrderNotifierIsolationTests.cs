namespace PingMe.IntegrationTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PingMe.Api.Contracts.Auth;
using PingMe.Api.Realtime;
using PingMe.Application.Ordering;
using PingMe.IntegrationTests.Infrastructure;
using Xunit;

public class OrderNotifierIsolationTests : IClassFixture<PingMeWebApplicationFactory>
{
    private readonly PingMeWebApplicationFactory _factory;

    public OrderNotifierIsolationTests(PingMeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid TenantId, string Token)> RegisterTenantAsync(string venueName)
    {
        var client = _factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/auth/register-tenant",
            new RegisterTenantRequest(venueName, email, "P@ssw0rd123"));
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth!.Token);
        var tenantId = Guid.Parse(jwt.Claims.First(c => c.Type == "tenantId").Value);

        return (tenantId, auth.Token);
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
    public async Task Broadcast_to_tenant_As_group_is_not_received_by_a_tenant_B_connection()
    {
        var tenantA = await RegisterTenantAsync("Venue A");
        var tenantB = await RegisterTenantAsync("Venue B");

        await using var connectionA = BuildHubConnection(tenantA.Token);
        await using var connectionB = BuildHubConnection(tenantB.Token);

        var tenantAReceived = new TaskCompletionSource<string>();
        var tenantBReceived = false;
        connectionA.On<string>("TestPing", message => tenantAReceived.TrySetResult(message));
        connectionB.On<string>("TestPing", _ => tenantBReceived = true);

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        using var scope = _factory.Services.CreateScope();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<OrdersHub>>();
        await hubContext.Clients.Group($"tenant-{tenantA.TenantId}").SendAsync("TestPing", "hello-a");

        var completed = await Task.WhenAny(tenantAReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(tenantAReceived.Task, completed);
        Assert.Equal("hello-a", await tenantAReceived.Task);
        Assert.False(tenantBReceived, "Tenant B's connection must never receive a broadcast scoped to Tenant A's group.");
    }

    [Fact]
    public async Task NotifyOrderReceivedAsync_delivers_only_to_the_correct_tenants_group()
    {
        var tenantA = await RegisterTenantAsync("Venue A");
        var tenantB = await RegisterTenantAsync("Venue B");

        await using var connectionA = BuildHubConnection(tenantA.Token);
        await using var connectionB = BuildHubConnection(tenantB.Token);

        var received = new TaskCompletionSource<AdminOrderDto>();
        var tenantBReceived = false;
        connectionA.On<AdminOrderDto>("OrderReceived", order => received.TrySetResult(order));
        connectionB.On<AdminOrderDto>("OrderReceived", _ => tenantBReceived = true);

        await connectionA.StartAsync();
        await connectionB.StartAsync();

        using var scope = _factory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IOrderNotifier>();
        var dto = new AdminOrderDto(Guid.NewGuid(), "Received", DateTime.UtcNow, new List<AdminOrderItemDto>(), "NotConfigured");
        await notifier.NotifyOrderReceivedAsync(tenantA.TenantId, dto);

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(received.Task, completed);
        Assert.Equal(dto.Id, (await received.Task).Id);
        Assert.False(tenantBReceived);
    }
}
