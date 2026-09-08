namespace PingMe.Api.Contracts.Venues;

using PingMe.Domain.Locations;

public static class VenueThemeMapper
{
    public static VenueThemeDto ToThemeDto(this Venue venue) => new(
        venue.PrimaryColor,
        venue.AccentColor,
        venue.CurrencyCode,
        venue.ThemeMode.ToString(),
        venue.LogoUrl,
        venue.HeroImageUrl,
        venue.Tagline);
}
