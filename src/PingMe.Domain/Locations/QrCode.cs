namespace PingMe.Domain.Locations;

using PingMe.Domain.Common;

public class QrCode : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid LocationId { get; private set; }
    public string Code { get; private set; } = default!;

    private QrCode() { }

    public QrCode(Guid tenantId, Guid locationId, string code)
    {
        TenantId = tenantId;
        LocationId = locationId;
        Code = code;
    }
}
