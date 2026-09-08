namespace PingMe.Domain.Locations;

using System.Text.RegularExpressions;

/// <summary>
/// Validation helpers for tenant-supplied venue branding. Every value here ends up
/// rendered in a customer's browser, so it is validated before it is ever stored.
/// </summary>
public static partial class VenueBranding
{
    public const string DefaultPrimaryColor = "#111827";
    public const string DefaultAccentColor = "#F97316";
    public const string DefaultCurrencyCode = "EUR";

    public const int MaxTaglineLength = 80;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex CurrencyCodeRegex();

    public static bool IsValidHexColor(string? value) =>
        value is not null && HexColorRegex().IsMatch(value);

    public static bool IsValidCurrencyCode(string? value) =>
        value is not null && CurrencyCodeRegex().IsMatch(value);

    /// <summary>
    /// Image URLs are loaded by the customer's browser, so only absolute http(s) URLs are accepted.
    /// This blocks javascript:, data: and relative values that could be abused.
    /// </summary>
    public static bool IsValidImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static string NormalizeHexColor(string value) => value.ToUpperInvariant();

    public static string NormalizeCurrencyCode(string value) => value.ToUpperInvariant();
}
