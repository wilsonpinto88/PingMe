namespace PingMe.Application.Integrations;

public interface IPosIntegrationResolver
{
    Task<IPosIntegration?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken);
}
