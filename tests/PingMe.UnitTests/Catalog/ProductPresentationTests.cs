namespace PingMe.UnitTests.Catalog;

using PingMe.Domain.Catalog;
using Xunit;

public class ProductPresentationTests
{
    private static Product NewProduct() => new(Guid.NewGuid(), Guid.NewGuid(), "Negroni", 9.50m);

    [Fact]
    public void Presentation_defaults_to_empty()
    {
        var product = NewProduct();

        Assert.Null(product.Description);
        Assert.Null(product.ImageUrl);
    }

    [Fact]
    public void Setting_presentation_trims_the_description()
    {
        var product = NewProduct();

        product.SetPresentation("  Gin, Campari, sweet vermouth  ", "https://cdn.example.com/negroni.jpg");

        Assert.Equal("Gin, Campari, sweet vermouth", product.Description);
        Assert.Equal("https://cdn.example.com/negroni.jpg", product.ImageUrl);
    }

    [Fact]
    public void A_non_http_image_url_is_rejected()
    {
        var product = NewProduct();

        Assert.Throws<ArgumentException>(() =>
            product.SetPresentation(null, "javascript:alert(1)"));
    }

    [Fact]
    public void A_description_over_the_limit_is_rejected()
    {
        var product = NewProduct();
        var tooLong = new string('a', Product.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentException>(() => product.SetPresentation(tooLong, null));
    }
}
