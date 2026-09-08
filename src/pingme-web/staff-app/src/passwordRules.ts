/**
 * Matches the Identity password rules configured in Program.cs (RequiredLength
 * 8, everything else left at Identity's default: upper, lower, digit, symbol).
 * Checked client-side only to give a fast, specific error before the round
 * trip — the API is still the authority and re-validates on submit.
 */
export function passwordIssue(password: string): string | null {
  if (password.length < 8) {
    return "Password must be at least 8 characters.";
  }
  if (!/[A-Z]/.test(password)) {
    return "Password must include an uppercase letter.";
  }
  if (!/[a-z]/.test(password)) {
    return "Password must include a lowercase letter.";
  }
  if (!/[0-9]/.test(password)) {
    return "Password must include a digit.";
  }
  if (!/[^A-Za-z0-9]/.test(password)) {
    return "Password must include a symbol.";
  }
  return null;
}
