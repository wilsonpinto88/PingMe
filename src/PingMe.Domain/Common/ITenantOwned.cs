namespace PingMe.Domain.Common;

public interface ITenantOwned
{
    Guid TenantId { get; }
}
