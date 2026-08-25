namespace PingMe.Infrastructure.Tenants;

using PingMe.Application.Tenants;

public class CurrentTenantProvider : ICurrentTenantProvider
{
    public Guid? TenantId { get; set; }
}
