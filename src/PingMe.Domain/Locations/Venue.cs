namespace PingMe.Domain.Locations;

using PingMe.Domain.Common;

public class Venue : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;

    // Branding — every field is rendered in the customer ordering app.
    public string PrimaryColor { get; private set; } = VenueBranding.DefaultPrimaryColor;
    public string AccentColor { get; private set; } = VenueBranding.DefaultAccentColor;
    public string CurrencyCode { get; private set; } = VenueBranding.DefaultCurrencyCode;
    public VenueThemeMode ThemeMode { get; private set; } = VenueThemeMode.Light;
    public string? LogoUrl { get; private set; }
    public string? HeroImageUrl { get; private set; }
    public string? Tagline { get; private set; }

    private Venue() { }

    public Venue(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
    }

    /// <summary>
    /// Applies validated branding. Callers must validate with <see cref="VenueBranding"/>
    /// first; this method rejects anything that slipped through rather than storing it.
    /// </summary>
    public void UpdateBranding(
        string primaryColor,
        string accentColor,
        string currencyCode,
        VenueThemeMode themeMode,
        string? logoUrl,
        string? heroImageUrl,
        string? tagline)
    {
        if (!VenueBranding.IsValidHexColor(primaryColor))
        {
            throw new ArgumentException("Primary color must be a #RRGGBB hex value.", nameof(primaryColor));
        }

        if (!VenueBranding.IsValidHexColor(accentColor))
        {
            throw new ArgumentException("Accent color must be a #RRGGBB hex value.", nameof(accentColor));
        }

        if (!VenueBranding.IsValidCurrencyCode(currencyCode))
        {
            throw new ArgumentException("Currency code must be a 3-letter ISO code.", nameof(currencyCode));
        }

        if (!Enum.IsDefined(themeMode))
        {
            throw new ArgumentException("Unknown theme mode.", nameof(themeMode));
        }

        if (logoUrl is not null && !VenueBranding.IsValidImageUrl(logoUrl))
        {
            throw new ArgumentException("Logo URL must be an absolute http(s) URL.", nameof(logoUrl));
        }

        if (heroImageUrl is not null && !VenueBranding.IsValidImageUrl(heroImageUrl))
        {
            throw new ArgumentException("Hero image URL must be an absolute http(s) URL.", nameof(heroImageUrl));
        }

        if (tagline is not null && tagline.Length > VenueBranding.MaxTaglineLength)
        {
            throw new ArgumentException(
                $"Tagline must be {VenueBranding.MaxTaglineLength} characters or fewer.", nameof(tagline));
        }

        PrimaryColor = VenueBranding.NormalizeHexColor(primaryColor);
        AccentColor = VenueBranding.NormalizeHexColor(accentColor);
        CurrencyCode = VenueBranding.NormalizeCurrencyCode(currencyCode);
        ThemeMode = themeMode;
        LogoUrl = logoUrl;
        HeroImageUrl = heroImageUrl;
        Tagline = string.IsNullOrWhiteSpace(tagline) ? null : tagline.Trim();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Venue name is required.", nameof(name));
        }

        Name = name.Trim();
    }
}
