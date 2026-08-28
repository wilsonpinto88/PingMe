namespace PingMe.Domain.Integrations;

using PingMe.Domain.Common;

public class TenantPosIntegrationSettings : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public string WebhookUrl { get; private set; } = default!;
    public bool IsEnabled { get; private set; }

    private TenantPosIntegrationSettings() { }

    public TenantPosIntegrationSettings(Guid tenantId, ProviderType providerType, string webhookUrl, bool isEnabled)
    {
        TenantId = tenantId;
        ProviderType = providerType;
        WebhookUrl = webhookUrl;
        IsEnabled = isEnabled;
    }

    public void UpdateSettings(ProviderType providerType, string webhookUrl, bool isEnabled)
    {
        ProviderType = providerType;
        WebhookUrl = webhookUrl;
        IsEnabled = isEnabled;
    }
}
