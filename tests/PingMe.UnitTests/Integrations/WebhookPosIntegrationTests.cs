namespace PingMe.UnitTests.Integrations;

using System.Net;
using System.Text.Json;
using PingMe.Domain.Ordering;
using Xunit;

public class WebhookPosIntegrationTests
{
    private class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Posts_the_order_snapshot_as_camelCase_JSON_to_the_configured_url()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var integration = new PingMe.Infrastructure.Integrations.WebhookPosIntegration(
            "http://example.invalid/webhook", httpClient);

        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        order.AddItem(Guid.NewGuid(), "Burger", 9.50m, 2);
        order.AddItem(Guid.NewGuid(), "Beer", 4.00m, 2);

        await integration.SendOrderAsync(order, "Table 12", CancellationToken.None);

        Assert.NotNull(handler.CapturedRequest);
        Assert.Equal(HttpMethod.Post, handler.CapturedRequest!.Method);
        Assert.Equal("http://example.invalid/webhook", handler.CapturedRequest.RequestUri!.ToString());

        using var payload = JsonDocument.Parse(handler.CapturedBody!);
        var root = payload.RootElement;
        Assert.Equal("order.created", root.GetProperty("eventType").GetString());
        Assert.NotEqual(Guid.Empty, root.GetProperty("eventId").GetGuid());
        Assert.True(root.GetProperty("occurredAt").TryGetDateTime(out _));
        Assert.Equal(order.TenantId, root.GetProperty("tenantId").GetGuid());

        var orderElement = root.GetProperty("order");
        Assert.Equal(order.Id, orderElement.GetProperty("id").GetGuid());
        Assert.Equal("Table 12", orderElement.GetProperty("locationLabel").GetString());
        Assert.Equal(27.00m, orderElement.GetProperty("total").GetDecimal());

        var items = orderElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Burger", items[0].GetProperty("name").GetString());
        Assert.Equal(2, items[0].GetProperty("quantity").GetInt32());
        Assert.Equal(9.50m, items[0].GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public async Task Generates_a_distinct_eventId_per_delivery_attempt()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var integration = new PingMe.Infrastructure.Integrations.WebhookPosIntegration(
            "http://example.invalid/webhook", httpClient);

        var order = new Order(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        await integration.SendOrderAsync(order, "Table 1", CancellationToken.None);
        var firstEventId = JsonDocument.Parse(handler.CapturedBody!).RootElement.GetProperty("eventId").GetGuid();

        await integration.SendOrderAsync(order, "Table 1", CancellationToken.None);
        var secondEventId = JsonDocument.Parse(handler.CapturedBody!).RootElement.GetProperty("eventId").GetGuid();

        Assert.NotEqual(firstEventId, secondEventId);
    }
}
