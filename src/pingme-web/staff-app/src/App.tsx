import { useCallback, useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { getOrders, login, updateOrderStatus } from "./api";
import { connectOrdersHub } from "./signalr";
import { ACTIVE_STATUSES, groupByStatus, upsertById, type ActiveStatus } from "./orders";
import { OrderList } from "./components/OrderList";
import { RegisterForm } from "./components/RegisterForm";
import { useMediaQuery } from "./useMediaQuery";
import type { AdminOrderDto, ConnectionState } from "./types";

const TOKEN_KEY = "pingme.staff.token";

/** Waiting times are minute-resolution, so a 30s tick keeps them honest cheaply. */
const CLOCK_TICK_MS = 30_000;

const COLUMN_TITLES: Record<ActiveStatus, string> = {
  Received: "New",
  Accepted: "Accepted",
  Preparing: "Preparing",
  Ready: "Ready",
};

/** Tab-scoped so a mid-shift refresh does not force a re-login, but closing the tab does. */
function readStoredToken(): string | null {
  try {
    return window.sessionStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

function storeToken(token: string | null): void {
  try {
    if (token) {
      window.sessionStorage.setItem(TOKEN_KEY, token);
    } else {
      window.sessionStorage.removeItem(TOKEN_KEY);
    }
  } catch {
    // Storage being unavailable must not block signing in.
  }
}

export default function App() {
  const [token, setToken] = useState<string | null>(readStoredToken);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [signingIn, setSigningIn] = useState(false);
  const [loginError, setLoginError] = useState<string | null>(null);
  const [mode, setMode] = useState<"signIn" | "register">("signIn");

  const [orders, setOrders] = useState<AdminOrderDto[]>([]);
  const [loadingOrders, setLoadingOrders] = useState(false);
  const [connection, setConnection] = useState<ConnectionState>("disconnected");
  const [errorByOrderId, setErrorByOrderId] = useState<Record<string, string | undefined>>({});
  const [pendingOrderIds, setPendingOrderIds] = useState<Set<string>>(new Set());
  const [visibleStatus, setVisibleStatus] = useState<ActiveStatus>("Received");
  const [now, setNow] = useState(() => Date.now());

  // Matches the breakpoint where the stylesheet switches to four columns.
  const isWideScreen = useMediaQuery("(min-width: 900px)");

  const hubRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    const interval = setInterval(() => setNow(Date.now()), CLOCK_TICK_MS);
    return () => clearInterval(interval);
  }, []);

  const signOut = useCallback(() => {
    storeToken(null);
    setToken(null);
    setOrders([]);
    setConnection("disconnected");
    void hubRef.current?.stop();
    hubRef.current = null;
  }, []);

  // Opening the board is one unit of work: subscribe first, then backfill, so
  // an order placed during the initial fetch cannot slip through the gap.
  useEffect(() => {
    if (!token) {
      return;
    }

    let cancelled = false;
    setLoadingOrders(true);

    const open = async () => {
      try {
        const hub = await connectOrdersHub(
          token,
          (conn) => {
            conn.on("OrderReceived", (order: AdminOrderDto) => {
              setOrders((current) => upsertById(current, order));
            });
            conn.on("OrderStatusChanged", (order: AdminOrderDto) => {
              setOrders((current) => upsertById(current, order));
            });
          },
          (state) => {
            if (!cancelled) {
              setConnection(state);
            }
          },
        );
        hubRef.current = hub;

        const initial = await getOrders(token);
        if (!cancelled) {
          setOrders((current) => initial.reduce(upsertById, current));
        }
      } catch {
        if (!cancelled) {
          // The stored token is the usual culprit here: expired, or from a
          // previous deployment. Send staff back to the sign-in screen.
          setConnection("disconnected");
          setLoginError("Your session has ended. Sign in again to see live orders.");
          storeToken(null);
          setToken(null);
        }
      } finally {
        if (!cancelled) {
          setLoadingOrders(false);
        }
      }
    };

    void open();

    return () => {
      cancelled = true;
      void hubRef.current?.stop();
      hubRef.current = null;
    };
  }, [token]);

  const handleLogin = async (event: React.FormEvent) => {
    event.preventDefault();
    setLoginError(null);
    setSigningIn(true);
    try {
      const auth = await login(email, password);
      storeToken(auth.token);
      setPassword("");
      setToken(auth.token);
    } catch {
      setLoginError("That email and password did not match. Check them and try again.");
    } finally {
      setSigningIn(false);
    }
  };

  const handleAdvanceStatus = async (orderId: string, nextStatus: string) => {
    if (!token) {
      return;
    }

    setPendingOrderIds((current) => new Set(current).add(orderId));
    setErrorByOrderId((current) => ({ ...current, [orderId]: undefined }));

    try {
      await updateOrderStatus(token, orderId, nextStatus);
      // The authoritative order comes back over the hub, so nothing is written
      // to local state here: no chance of the board disagreeing with the API.
    } catch {
      setErrorByOrderId((current) => ({
        ...current,
        [orderId]: "Could not update this order. It may have already moved on.",
      }));
    } finally {
      setPendingOrderIds((current) => {
        const next = new Set(current);
        next.delete(orderId);
        return next;
      });
    }
  };

  if (!token) {
    if (mode === "register") {
      return (
        <main className="login">
          <RegisterForm
            onRegistered={(newToken) => {
              storeToken(newToken);
              setToken(newToken);
            }}
            onSwitchToSignIn={() => setMode("signIn")}
          />
        </main>
      );
    }

    return (
      <main className="login">
        <form className="login__card" onSubmit={handleLogin}>
          <div>
            <h1 className="login__title">PingMe Staff</h1>
            <p className="login__subtitle">Sign in to see orders as they come in.</p>
          </div>

          <div className="field">
            <label className="field__label" htmlFor="email">
              Email
            </label>
            <input
              className="field__input"
              id="email"
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
            <label className="field__label" htmlFor="password">
              Password
            </label>
            <input
              className="field__input"
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </div>

          {loginError && (
            <p className="notice notice--error" role="alert">
              {loginError}
            </p>
          )}

          <button className="btn btn--advance btn--block" type="submit" disabled={signingIn}>
            {signingIn ? "Signing in..." : "Sign in"}
          </button>

          <button
            type="button"
            className="btn btn--ghost btn--block"
            onClick={() => setMode("register")}
          >
            New venue? Register
          </button>
        </form>
      </main>
    );
  }

  const grouped = groupByStatus(orders);

  return (
    <>
      <header className="topbar">
        <h1 className="topbar__title">Orders</h1>
        <span className="topbar__spacer" />
        <span className="conn" data-state={connection} role="status">
          <span className="conn__dot" aria-hidden="true" />
          {connection === "connected"
            ? "Live"
            : connection === "reconnecting"
              ? "Reconnecting"
              : connection === "connecting"
                ? "Connecting"
                : "Offline"}
        </span>
        <button type="button" className="btn btn--ghost" onClick={signOut}>
          Sign out
        </button>
      </header>

      {connection === "disconnected" && (
        <div style={{ padding: "0 var(--space-4)" }}>
          <p className="notice notice--error" role="alert">
            Not receiving live updates. Reload the page to reconnect.
          </p>
        </div>
      )}

      {/* Narrow screens get one status at a time rather than four cramped columns. */}
      <div className="filters" role="group" aria-label="Filter by status">
        {ACTIVE_STATUSES.map((status) => (
          <button
            key={status}
            type="button"
            className="chip"
            aria-pressed={visibleStatus === status}
            onClick={() => setVisibleStatus(status)}
          >
            {COLUMN_TITLES[status]}
            <span className="chip__count">{grouped[status].length}</span>
          </button>
        ))}
      </div>

      <main>
        {loadingOrders && orders.length === 0 ? (
          <div className="board" aria-hidden="true">
            <div className="column__list">
              <div className="skeleton" />
              <div className="skeleton" />
              <div className="skeleton" />
            </div>
          </div>
        ) : (
          <>
            <OrderList
              orders={orders}
              now={now}
              visibleStatus={isWideScreen ? null : visibleStatus}
              pendingOrderIds={pendingOrderIds}
              errorByOrderId={errorByOrderId}
              onAdvanceStatus={handleAdvanceStatus}
            />
            <p className="visually-hidden" aria-live="polite">
              {grouped.Received.length} new orders waiting.
            </p>
          </>
        )}
      </main>
    </>
  );
}
