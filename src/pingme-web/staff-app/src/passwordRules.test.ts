import { describe, expect, it } from "vitest";
import { passwordIssue } from "./passwordRules";

describe("passwordIssue", () => {
  it("accepts a password meeting every rule", () => {
    expect(passwordIssue("Str0ng!Pass")).toBeNull();
  });

  it("rejects a password under 8 characters", () => {
    expect(passwordIssue("Sh0rt!")).toBe("Password must be at least 8 characters.");
  });

  it("rejects a password with no uppercase letter", () => {
    expect(passwordIssue("str0ng!pass")).toBe("Password must include an uppercase letter.");
  });

  it("rejects a password with no lowercase letter", () => {
    expect(passwordIssue("STR0NG!PASS")).toBe("Password must include a lowercase letter.");
  });

  it("rejects a password with no digit", () => {
    expect(passwordIssue("Strong!Pass")).toBe("Password must include a digit.");
  });

  it("rejects a password with no symbol", () => {
    expect(passwordIssue("Str0ngPass")).toBe("Password must include a symbol.");
  });

  it("checks length before any other rule", () => {
    // Too short AND missing every other rule — length should win so the
    // message doesn't jump around as the user keeps typing.
    expect(passwordIssue("abc")).toBe("Password must be at least 8 characters.");
  });
});
