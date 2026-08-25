namespace PingMe.Domain.Catalog;

using PingMe.Domain.Common;

public class ProductOption : Entity
{
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal PriceDelta { get; private set; }

    private ProductOption() { }

    public ProductOption(Guid productId, string name, decimal priceDelta)
    {
        ProductId = productId;
        Name = name;
        PriceDelta = priceDelta;
    }
}
