namespace PingMe.Domain.Catalog;

using PingMe.Domain.Common;
using PingMe.Domain.Locations;

public class Product : Entity, ITenantOwned
{
    public const int MaxDescriptionLength = 280;

    public Guid TenantId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; }

    /// <summary>Short customer-facing blurb shown under the product name.</summary>
    public string? Description { get; private set; }

    /// <summary>Absolute http(s) image URL rendered in the customer app.</summary>
    public string? ImageUrl { get; private set; }

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

    /// <summary>
    /// Sets the optional presentation fields. The image URL is rendered in a customer's
    /// browser, so only absolute http(s) URLs are accepted.
    /// </summary>
    public void SetPresentation(string? description, string? imageUrl)
    {
        if (description is not null && description.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"Description must be {MaxDescriptionLength} characters or fewer.", nameof(description));
        }

        if (imageUrl is not null && !VenueBranding.IsValidImageUrl(imageUrl))
        {
            throw new ArgumentException("Image URL must be an absolute http(s) URL.", nameof(imageUrl));
        }

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ImageUrl = imageUrl;
    }
}
