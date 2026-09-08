import { useState } from "react";
import { ApiError, registerTenant } from "../api";
import { passwordIssue } from "../passwordRules";

interface RegisterFormProps {
  onRegistered: (token: string) => void;
  onSwitchToSignIn: () => void;
}

export function RegisterForm({ onRegistered, onSwitchToSignIn }: RegisterFormProps) {
  const [venueName, setVenueName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setError(null);

    if (password !== confirmPassword) {
      setError("Passwords don't match.");
      return;
    }

    const issue = passwordIssue(password);
    if (issue) {
      setError(issue);
      return;
    }

    setSubmitting(true);
    try {
      const auth = await registerTenant(venueName, email, password);
      onRegistered(auth.token);
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : "Could not create your venue. Check your connection and try again.",
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form className="login__card" onSubmit={handleSubmit}>
      <div>
        <h1 className="login__title">Register your venue</h1>
        <p className="login__subtitle">Create an owner account to start taking orders.</p>
      </div>

      <div className="field">
        <label className="field__label" htmlFor="venueName">
          Venue name
        </label>
        <input
          className="field__input"
          id="venueName"
          name="venueName"
          type="text"
          autoComplete="organization"
          required
          value={venueName}
          onChange={(e) => setVenueName(e.target.value)}
          placeholder="The Rooftop"
        />
      </div>

      <div className="field">
        <label className="field__label" htmlFor="registerEmail">
          Owner email
        </label>
        <input
          className="field__input"
          id="registerEmail"
          name="email"
          type="email"
          autoComplete="username"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="jane@venue.com"
        />
      </div>

      <div className="field">
        <label className="field__label" htmlFor="registerPassword">
          Password
        </label>
        <input
          className="field__input"
          id="registerPassword"
          name="password"
          type="password"
          autoComplete="new-password"
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
        <p className="notice notice--info notice--inline">
          At least 8 characters, with an uppercase letter, a lowercase letter, a digit, and a
          symbol.
        </p>
      </div>

      <div className="field">
        <label className="field__label" htmlFor="confirmPassword">
          Confirm password
        </label>
        <input
          className="field__input"
          id="confirmPassword"
          name="confirmPassword"
          type="password"
          autoComplete="new-password"
          required
          value={confirmPassword}
          onChange={(e) => setConfirmPassword(e.target.value)}
        />
      </div>

      {error && (
        <p className="notice notice--error" role="alert">
          {error}
        </p>
      )}

      <button className="btn btn--advance btn--block" type="submit" disabled={submitting}>
        {submitting ? "Creating your venue..." : "Create venue"}
      </button>

      <button type="button" className="btn btn--ghost btn--block" onClick={onSwitchToSignIn}>
        Already have an account? Sign in
      </button>
    </form>
  );
}
