namespace PingMe.Infrastructure.Integrations;

using Microsoft.Extensions.Logging;
using PingMe.Application.Integrations;
using PingMe.Domain.Integrations;
using PingMe.Domain.Ordering;

public class PosOrderDispatcher : IPosOrderDispatcher
{
    private readonly IPosIntegrationResolver _resolver;
    private readonly ILogger<PosOrderDispatcher> _logger;

    public PosOrderDispatcher(IPosIntegrationResolver resolver, ILogger<PosOrderDispatcher> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<PosDeliveryStatus> TryDispatchAsync(Order order, string locationLabel, CancellationToken cancellationToken)
    {
        try
        {
            var integration = await _resolver.ResolveAsync(order.TenantId, cancellationToken);
            if (integration is null)
            {
                return PosDeliveryStatus.NotConfigured;
            }

            await integration.SendOrderAsync(order, locationLabel, cancellationToken);
            return PosDeliveryStatus.Sent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "POS dispatch failed for order {OrderId}", order.Id);
            return PosDeliveryStatus.Failed;
        }
    }
}
