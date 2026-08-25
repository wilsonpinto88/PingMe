namespace PingMe.Domain.Catalog;

using PingMe.Domain.Common;

public class Category : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid MenuId { get; private set; }
    public string Name { get; private set; } = default!;
    public int SortOrder { get; private set; }

    private Category() { }

    public Category(Guid tenantId, Guid menuId, string name, int sortOrder)
    {
        TenantId = tenantId;
        MenuId = menuId;
        Name = name;
        SortOrder = sortOrder;
    }
}
