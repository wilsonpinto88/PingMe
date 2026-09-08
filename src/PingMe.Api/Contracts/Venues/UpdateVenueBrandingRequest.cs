namespace PingMe.Api.Contracts.Venues;

public record UpdateVenueBrandingRequest(
    string PrimaryColor,
    string AccentColor,
    string CurrencyCode,
    string ThemeMode,
    string? LogoUrl,
    string? HeroImageUrl,
    string? Tagline);
