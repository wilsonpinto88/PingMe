namespace PingMe.Infrastructure.Integrations;

using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using PingMe.Application.Integrations;
using PingMe.Domain.Integrations;
using PingMe.Infrastructure.Persistence;

public class PosIntegrationResolver : IPosIntegrationResolver
{
    private readonly PingMeDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public PosIntegrationResolver(PingMeDbContext dbContext, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IPosIntegration?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.TenantPosIntegrationSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);

        if (settings is null || !settings.IsEnabled)
        {
            return null;
        }

        return settings.ProviderType switch
        {
            ProviderType.Webhook => new WebhookPosIntegration(
                settings.WebhookUrl,
                _httpClientFactory.CreateClient(nameof(WebhookPosIntegration))),
            _ => null,
        };
    }
}
