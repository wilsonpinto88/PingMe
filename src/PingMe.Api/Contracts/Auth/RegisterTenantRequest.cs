namespace PingMe.Api.Contracts.Auth;

public record RegisterTenantRequest(string TenantName, string OwnerEmail, string OwnerPassword);
