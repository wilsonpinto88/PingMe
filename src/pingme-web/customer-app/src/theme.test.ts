import { describe, expect, it } from "vitest";
import {
  contrastRatio,
  FALLBACK_THEME,
  isValidHexColor,
  normalizeTheme,
  readableForeground,
} from "./theme";

describe("readableForeground", () => {
  it("puts white text on a dark venue colour", () => {
    expect(readableForeground("#111827")).toBe("#FFFFFF");
  });

  it("puts dark text on a pale venue colour", () => {
    expect(readableForeground("#FDE68A")).toBe("#16130F");
  });

  it("always clears the WCAG AA large-text threshold", () => {
    const brandColors = ["#111827", "#FDE68A", "#F97316", "#0F766E", "#FFFFFF", "#000000"];
    for (const color of brandColors) {
      expect(contrastRatio(color, readableForeground(color))).toBeGreaterThanOrEqual(3);
    }
  });

  it("falls back to white when the colour is not a valid hex value", () => {
    expect(readableForeground("rebeccapurple")).toBe("#FFFFFF");
  });
});

describe("isValidHexColor", () => {
  it.each(["#111827", "#fde68a", "#FFFFFF"])("accepts %s", (value) => {
    expect(isValidHexColor(value)).toBe(true);
  });

  it.each(["#FFF", "111827", "red", "", null, undefined])("rejects %s", (value) => {
    expect(isValidHexColor(value as string)).toBe(false);
  });
});

describe("normalizeTheme", () => {
  it("returns the fallback theme when the API sends nothing", () => {
    expect(normalizeTheme(null)).toEqual(FALLBACK_THEME);
  });

  it("keeps the good fields when only one is malformed", () => {
    const theme = normalizeTheme({
      primaryColor: "not-a-color",
      accentColor: "#0F766E",
      currencyCode: "gbp",
      themeMode: "Dark",
      logoUrl: null,
      heroImageUrl: null,
      tagline: null,
    });

    expect(theme.primaryColor).toBe(FALLBACK_THEME.primaryColor);
    expect(theme.accentColor).toBe("#0F766E");
    expect(theme.currencyCode).toBe("GBP");
    expect(theme.themeMode).toBe("Dark");
  });

  it("drops an image URL that is not http or https", () => {
    const theme = normalizeTheme({
      ...FALLBACK_THEME,
      logoUrl: "javascript:alert(1)",
      heroImageUrl: "https://cdn.example.com/hero.jpg",
    });

    expect(theme.logoUrl).toBeNull();
    expect(theme.heroImageUrl).toBe("https://cdn.example.com/hero.jpg");
  });

  it("treats an unknown theme mode as light", () => {
    expect(normalizeTheme({ ...FALLBACK_THEME, themeMode: "Neon" as never }).themeMode).toBe(
      "Light",
    );
  });

  it("treats a blank tagline as absent", () => {
    expect(normalizeTheme({ ...FALLBACK_THEME, tagline: "   " }).tagline).toBeNull();
  });
});
