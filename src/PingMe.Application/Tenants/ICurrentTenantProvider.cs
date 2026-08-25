namespace PingMe.Application.Tenants;

public interface ICurrentTenantProvider
{
    Guid? TenantId { get; }
}
