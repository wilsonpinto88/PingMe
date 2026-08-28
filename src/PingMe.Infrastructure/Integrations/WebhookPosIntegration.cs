namespace PingMe.Infrastructure.Integrations;

using System.Net.Http.Json;
using System.Text.Json;
using PingMe.Application.Integrations;
using PingMe.Domain.Ordering;

public class WebhookPosIntegration : IPosIntegration
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _webhookUrl;
    private readonly HttpClient _httpClient;

    public WebhookPosIntegration(string webhookUrl, HttpClient httpClient)
    {
        _webhookUrl = webhookUrl;
        _httpClient = httpClient;
    }

    public async Task SendOrderAsync(Order order, string locationLabel, CancellationToken cancellationToken)
    {
        var orderPayload = new WebhookOrderPayload(
            order.Id,
            locationLabel,
            order.Items
                .Select(i => new WebhookOrderItemPayload(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
                .ToList(),
            order.Items.Sum(i => i.UnitPrice * i.Quantity));

        var envelope = new WebhookEventEnvelope(
            Guid.NewGuid(),
            "order.created",
            DateTime.UtcNow,
            order.TenantId,
            orderPayload);

        var response = await _httpClient.PostAsJsonAsync(_webhookUrl, envelope, PayloadJsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private record WebhookEventEnvelope(Guid EventId, string EventType, DateTime OccurredAt, Guid TenantId, WebhookOrderPayload Order);

    private record WebhookOrderPayload(Guid Id, string LocationLabel, List<WebhookOrderItemPayload> Items, decimal Total);

    private record WebhookOrderItemPayload(Guid ProductId, string Name, int Quantity, decimal UnitPrice);
}
