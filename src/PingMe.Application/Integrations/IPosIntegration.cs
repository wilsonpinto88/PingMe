namespace PingMe.Application.Integrations;

using PingMe.Domain.Ordering;

public interface IPosIntegration
{
    Task SendOrderAsync(Order order, string locationLabel, CancellationToken cancellationToken);
}
