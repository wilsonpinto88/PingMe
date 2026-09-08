import type { VenueTheme } from "./types";

/**
 * Venue branding is tenant-supplied, so a venue can pick any brand colour it
 * likes. Everything here exists to make an arbitrary colour safe to render:
 * text placed on it is chosen for contrast rather than hardcoded to white.
 */

const LIGHT_FOREGROUND = "#FFFFFF";
const DARK_FOREGROUND = "#16130F";

export const FALLBACK_THEME: VenueTheme = {
  primaryColor: "#111827",
  accentColor: "#F97316",
  currencyCode: "EUR",
  themeMode: "Light",
  logoUrl: null,
  heroImageUrl: null,
  tagline: null,
};

const HEX_PATTERN = /^#[0-9a-f]{6}$/i;

export function isValidHexColor(value: string | null | undefined): boolean {
  return typeof value === "string" && HEX_PATTERN.test(value);
}

export function hexToRgb(hex: string): [number, number, number] {
  const value = hex.replace("#", "");
  return [
    parseInt(value.slice(0, 2), 16),
    parseInt(value.slice(2, 4), 16),
    parseInt(value.slice(4, 6), 16),
  ];
}

/** WCAG relative luminance. */
export function relativeLuminance(hex: string): number {
  const channels = hexToRgb(hex).map((channel) => {
    const c = channel / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}

export function contrastRatio(a: string, b: string): number {
  const lighter = Math.max(relativeLuminance(a), relativeLuminance(b));
  const darker = Math.min(relativeLuminance(a), relativeLuminance(b));
  return (lighter + 0.05) / (darker + 0.05);
}

/**
 * Picks black or white text for a background, whichever gives more contrast.
 * A venue that chooses a pale yellow brand colour still gets readable buttons.
 */
export function readableForeground(background: string): string {
  if (!isValidHexColor(background)) {
    return LIGHT_FOREGROUND;
  }
  return contrastRatio(background, DARK_FOREGROUND) >=
    contrastRatio(background, LIGHT_FOREGROUND)
    ? DARK_FOREGROUND
    : LIGHT_FOREGROUND;
}

function withAlpha(hex: string, alpha: number): string {
  const [r, g, b] = hexToRgb(hex);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

/**
 * Falls back field by field rather than all-or-nothing, so one bad colour from
 * the API cannot leave the app unstyled.
 */
export function normalizeTheme(theme: Partial<VenueTheme> | null | undefined): VenueTheme {
  return {
    primaryColor: isValidHexColor(theme?.primaryColor)
      ? theme!.primaryColor!
      : FALLBACK_THEME.primaryColor,
    accentColor: isValidHexColor(theme?.accentColor)
      ? theme!.accentColor!
      : FALLBACK_THEME.accentColor,
    currencyCode:
      typeof theme?.currencyCode === "string" && /^[A-Za-z]{3}$/.test(theme.currencyCode)
        ? theme.currencyCode.toUpperCase()
        : FALLBACK_THEME.currencyCode,
    themeMode: theme?.themeMode === "Dark" ? "Dark" : "Light",
    logoUrl: safeImageUrl(theme?.logoUrl),
    heroImageUrl: safeImageUrl(theme?.heroImageUrl),
    tagline: typeof theme?.tagline === "string" && theme.tagline.trim() ? theme.tagline : null,
  };
}

/**
 * The API validates image URLs on write, but this app also renders them, so it
 * re-checks the scheme rather than trusting the response.
 */
function safeImageUrl(value: string | null | undefined): string | null {
  if (typeof value !== "string" || !value) {
    return null;
  }
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:" ? value : null;
  } catch {
    return null;
  }
}

/** Writes the venue branding into the CSS custom properties the stylesheet reads. */
export function applyTheme(theme: VenueTheme, root: HTMLElement = document.documentElement): void {
  const style = root.style;
  style.setProperty("--brand-primary", theme.primaryColor);
  style.setProperty("--brand-on-primary", readableForeground(theme.primaryColor));
  style.setProperty("--brand-accent", theme.accentColor);
  style.setProperty("--brand-on-accent", readableForeground(theme.accentColor));
  style.setProperty("--brand-primary-soft", withAlpha(theme.primaryColor, 0.08));

  root.setAttribute("data-theme", theme.themeMode === "Dark" ? "dark" : "light");
  root.style.colorScheme = theme.themeMode === "Dark" ? "dark" : "light";

  // Tints the mobile browser chrome to match the venue.
  const meta = document.querySelector('meta[name="theme-color"]');
  if (meta) {
    meta.setAttribute("content", theme.primaryColor);
  }
}
