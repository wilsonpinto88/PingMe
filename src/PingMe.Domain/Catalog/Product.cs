namespace PingMe.Domain.Catalog;

using PingMe.Domain.Common;

public class Product : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; }

    private Product() { }

    public Product(Guid tenantId, Guid categoryId, string name, decimal price, bool isAvailable = true)
    {
        TenantId = tenantId;
        CategoryId = categoryId;
        Name = name;
        Price = price;
        IsAvailable = isAvailable;
    }

    public void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
    }
}
