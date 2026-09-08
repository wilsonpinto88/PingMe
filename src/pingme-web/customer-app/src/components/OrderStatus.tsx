import { useEffect, useState } from "react";
import { getOrderStatus } from "../api";
import { headlineFor, isComplete, noteFor, progressSteps } from "../orderProgress";

interface OrderStatusProps {
  orderId: string;
  sessionId: string;
  locationLabel: string;
  onStartNewOrder: () => void;
}

const POLL_INTERVAL_MS = 5000;

/** Stops nagging the customer about a blip; only a sustained outage is worth showing. */
const FAILURES_BEFORE_WARNING = 3;

export function OrderStatus({
  orderId,
  sessionId,
  locationLabel,
  onStartNewOrder,
}: OrderStatusProps) {
  const [status, setStatus] = useState("Received");
  const [consecutiveFailures, setConsecutiveFailures] = useState(0);

  useEffect(() => {
    let cancelled = false;

    const poll = async () => {
      try {
        const result = await getOrderStatus(orderId, sessionId);
        if (!cancelled) {
          setStatus(result.status);
          setConsecutiveFailures(0);
        }
      } catch {
        if (!cancelled) {
          setConsecutiveFailures((count) => count + 1);
        }
      }
    };

    // Fetch straight away rather than showing a guessed status for five seconds.
    void poll();
    const interval = setInterval(poll, POLL_INTERVAL_MS);

    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [orderId, sessionId]);

  const steps = progressSteps(status);
  const stale = consecutiveFailures >= FAILURES_BEFORE_WARNING;

  return (
    <main className="shell status-screen" id="main">
      <div>
        <p className="status-eyebrow">
          {!stale && <span className="live-dot" aria-hidden="true" />}
          {stale ? "Reconnecting" : "Live"} &middot; {locationLabel}
        </p>
        <h1 className="status-headline">{headlineFor(status)}</h1>
      </div>

      <p className="status-note">{noteFor(status)}</p>

      <ol className="progress" aria-label="Order progress">
        {steps.map((step) => (
          <li className="progress__step" data-state={step.state} key={step.status}>
            <span className="progress__marker" aria-hidden="true" />
            <span>{step.label}</span>
            {step.state === "current" && <span className="visually-hidden"> (current step)</span>}
            {step.state === "done" && <span className="visually-hidden"> (completed)</span>}
          </li>
        ))}
      </ol>

      {/* Announced politely so a screen reader hears each transition. */}
      <p className="visually-hidden" aria-live="polite">
        Order status: {headlineFor(status)}
      </p>

      {stale && (
        <p className="notice notice--info" role="status">
          We are having trouble reaching the venue right now. Your order is still placed — this page
          will catch up as soon as the connection returns.
        </p>
      )}

      {isComplete(status) && (
        <button type="button" className="btn btn--primary btn--lg" onClick={onStartNewOrder}>
          Start another order
        </button>
      )}
    </main>
  );
}
