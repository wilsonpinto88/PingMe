namespace PingMe.UnitTests.Locations;

using PingMe.Domain.Locations;
using Xunit;

public class VenueBrandingTests
{
    private static Venue NewVenue() => new(Guid.NewGuid(), "The Rooftop");

    private static void ApplyValidBranding(Venue venue) =>
        venue.UpdateBranding("#0f766e", "#facc15", "gbp", VenueThemeMode.Dark, null, null, null);

    [Fact]
    public void A_new_venue_starts_with_usable_default_branding()
    {
        var venue = NewVenue();

        Assert.Equal(VenueBranding.DefaultPrimaryColor, venue.PrimaryColor);
        Assert.Equal(VenueBranding.DefaultAccentColor, venue.AccentColor);
        Assert.Equal(VenueBranding.DefaultCurrencyCode, venue.CurrencyCode);
        Assert.Equal(VenueThemeMode.Light, venue.ThemeMode);
    }

    [Fact]
    public void Updating_branding_normalizes_colors_and_currency_to_uppercase()
    {
        var venue = NewVenue();

        ApplyValidBranding(venue);

        Assert.Equal("#0F766E", venue.PrimaryColor);
        Assert.Equal("#FACC15", venue.AccentColor);
        Assert.Equal("GBP", venue.CurrencyCode);
        Assert.Equal(VenueThemeMode.Dark, venue.ThemeMode);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("0F766E")]
    public void An_invalid_primary_color_is_rejected(string color)
    {
        var venue = NewVenue();

        Assert.Throws<ArgumentException>(() =>
            venue.UpdateBranding(color, "#FACC15", "EUR", VenueThemeMode.Light, null, null, null));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,AAAA")]
    [InlineData("/logo.png")]
    [InlineData("ftp://example.com/logo.png")]
    public void A_logo_url_that_is_not_absolute_http_is_rejected(string url)
    {
        var venue = NewVenue();

        Assert.Throws<ArgumentException>(() =>
            venue.UpdateBranding("#0F766E", "#FACC15", "EUR", VenueThemeMode.Light, url, null, null));
    }

    [Fact]
    public void An_undefined_theme_mode_is_rejected()
    {
        var venue = NewVenue();

        Assert.Throws<ArgumentException>(() =>
            venue.UpdateBranding("#0F766E", "#FACC15", "EUR", (VenueThemeMode)99, null, null, null));
    }

    [Fact]
    public void A_tagline_longer_than_the_limit_is_rejected()
    {
        var venue = NewVenue();
        var tooLong = new string('a', VenueBranding.MaxTaglineLength + 1);

        Assert.Throws<ArgumentException>(() =>
            venue.UpdateBranding("#0F766E", "#FACC15", "EUR", VenueThemeMode.Light, null, null, tooLong));
    }

    [Fact]
    public void A_blank_tagline_is_stored_as_null_rather_than_whitespace()
    {
        var venue = NewVenue();

        venue.UpdateBranding("#0F766E", "#FACC15", "EUR", VenueThemeMode.Light, null, null, "   ");

        Assert.Null(venue.Tagline);
    }

    [Fact]
    public void Rejected_branding_leaves_the_previous_values_untouched()
    {
        var venue = NewVenue();
        ApplyValidBranding(venue);

        Assert.Throws<ArgumentException>(() =>
            venue.UpdateBranding("#0F766E", "not-a-color", "EUR", VenueThemeMode.Light, null, null, null));

        Assert.Equal("#0F766E", venue.PrimaryColor);
        Assert.Equal("#FACC15", venue.AccentColor);
    }
}
