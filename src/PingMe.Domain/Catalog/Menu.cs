namespace PingMe.Domain.Catalog;

using PingMe.Domain.Common;

public class Menu : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;

    private Menu() { }

    public Menu(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
    }
}
