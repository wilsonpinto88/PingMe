namespace PingMe.Application.Integrations;

using PingMe.Domain.Integrations;
using PingMe.Domain.Ordering;

public interface IPosOrderDispatcher
{
    Task<PosDeliveryStatus> TryDispatchAsync(Order order, string locationLabel, CancellationToken cancellationToken);
}
