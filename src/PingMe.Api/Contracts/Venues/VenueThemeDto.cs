namespace PingMe.Api.Contracts.Venues;

/// <summary>
/// The per-venue branding the customer app uses to theme itself.
/// </summary>
public record VenueThemeDto(
    string PrimaryColor,
    string AccentColor,
    string CurrencyCode,
    string ThemeMode,
    string? LogoUrl,
    string? HeroImageUrl,
    string? Tagline);
