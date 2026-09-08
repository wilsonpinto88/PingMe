/**
 * Prices are formatted in the venue currency rather than a hardcoded symbol,
 * so a venue in London shows pounds without a code change.
 */
export function formatMoney(amount: number, currencyCode: string, locale?: string): string {
  // navigator is absent outside the browser, so fall back to the runtime default.
  const resolvedLocale =
    locale ?? (typeof navigator !== "undefined" ? navigator.language : undefined);

  try {
    return new Intl.NumberFormat(resolvedLocale, {
      style: "currency",
      currency: currencyCode,
    }).format(amount);
  } catch {
    // An unknown currency code should never blank out a price.
    return `${currencyCode} ${amount.toFixed(2)}`;
  }
}
