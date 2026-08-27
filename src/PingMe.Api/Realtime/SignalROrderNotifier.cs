namespace PingMe.Api.Realtime;

using Microsoft.AspNetCore.SignalR;
using PingMe.Application.Ordering;

public class SignalROrderNotifier : IOrderNotifier
{
    private readonly IHubContext<OrdersHub> _hubContext;
    private readonly ILogger<SignalROrderNotifier> _logger;

    public SignalROrderNotifier(IHubContext<OrdersHub> hubContext, ILogger<SignalROrderNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyOrderReceivedAsync(Guid tenantId, AdminOrderDto order)
    {
        try
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}").SendAsync("OrderReceived", order);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast OrderReceived for order {OrderId}", order.Id);
        }
    }

    public async Task NotifyOrderStatusChangedAsync(Guid tenantId, AdminOrderDto order)
    {
        try
        {
            await _hubContext.Clients.Group($"tenant-{tenantId}").SendAsync("OrderStatusChanged", order);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast OrderStatusChanged for order {OrderId}", order.Id);
        }
    }
}
