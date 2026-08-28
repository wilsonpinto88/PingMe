namespace PingMe.Api.Contracts.Integrations;

public record PosIntegrationSettingsDto(string ProviderType, string WebhookUrl, bool IsEnabled);
