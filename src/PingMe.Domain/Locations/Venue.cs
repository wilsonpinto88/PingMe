namespace PingMe.Domain.Locations;

using PingMe.Domain.Common;

public class Venue : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;

    private Venue() { }

    public Venue(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
    }
}
