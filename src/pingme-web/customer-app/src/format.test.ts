import { describe, expect, it } from "vitest";
import { formatMoney } from "./format";

describe("formatMoney", () => {
  it("formats in the venue currency rather than a fixed symbol", () => {
    expect(formatMoney(9.5, "GBP", "en-GB")).toBe("£9.50");
    expect(formatMoney(9.5, "USD", "en-US")).toBe("$9.50");
  });

  it("always shows two decimal places", () => {
    expect(formatMoney(9, "EUR", "de-DE")).toContain("9,00");
  });

  it("falls back to the code rather than throwing on an unknown currency", () => {
    expect(formatMoney(9.5, "ZZZZ", "en-GB")).toBe("ZZZZ 9.50");
  });
});
