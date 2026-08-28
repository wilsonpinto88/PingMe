namespace PingMe.Api.Contracts.Integrations;

public record UpsertPosIntegrationSettingsRequest(string ProviderType, string WebhookUrl, bool IsEnabled);
