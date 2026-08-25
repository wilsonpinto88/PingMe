namespace PingMe.Domain.Locations;

using PingMe.Domain.Common;

public class CustomerSession : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid LocationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    private CustomerSession() { }

    public CustomerSession(Guid tenantId, Guid locationId, DateTime createdAt, DateTime expiresAt)
    {
        TenantId = tenantId;
        LocationId = locationId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public bool IsOpen(DateTime now) => ClosedAt is null && now < ExpiresAt;

    public void Close(DateTime closedAt)
    {
        ClosedAt = closedAt;
    }
}
