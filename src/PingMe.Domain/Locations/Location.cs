namespace PingMe.Domain.Locations;

using PingMe.Domain.Common;

public class Location : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid VenueId { get; private set; }
    public Guid? ParentLocationId { get; private set; }
    public string Name { get; private set; } = default!;

    private Location() { }

    public Location(Guid tenantId, Guid venueId, string name, Guid? parentLocationId = null)
    {
        TenantId = tenantId;
        VenueId = venueId;
        Name = name;
        ParentLocationId = parentLocationId;
    }
}
