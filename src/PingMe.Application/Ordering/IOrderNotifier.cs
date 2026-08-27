namespace PingMe.Application.Ordering;

public interface IOrderNotifier
{
    Task NotifyOrderReceivedAsync(Guid tenantId, AdminOrderDto order);
    Task NotifyOrderStatusChangedAsync(Guid tenantId, AdminOrderDto order);
}
