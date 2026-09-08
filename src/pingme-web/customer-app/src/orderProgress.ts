export type StepState = "done" | "current" | "upcoming";

export interface ProgressStep {
  status: string;
  /** Customer-facing wording. The API status names are staff vocabulary. */
  label: string;
  state: StepState;
}

const STEPS: Array<{ status: string; label: string }> = [
  { status: "Received", label: "Order received" },
  { status: "Accepted", label: "Confirmed by the bar" },
  { status: "Preparing", label: "Being prepared" },
  { status: "Ready", label: "Ready" },
  { status: "Delivered", label: "Delivered to you" },
];

export const HEADLINES: Record<string, string> = {
  Received: "We have got your order",
  Accepted: "The bar has confirmed it",
  Preparing: "Your order is being made",
  Ready: "Almost with you",
  Delivered: "Enjoy",
};

export const NOTES: Record<string, string> = {
  Received: "Hang tight — staff are picking it up now. No need to queue.",
  Accepted: "Someone is on it. You will see each step update here.",
  Preparing: "It will be brought to you as soon as it is ready.",
  Ready: "Your order is on its way to your seat.",
  Delivered: "Your order has been delivered. Order again any time.",
};

/**
 * Maps the current status onto the fixed rail. An unknown status leaves every
 * step upcoming rather than throwing, so a new backend status cannot break the
 * screen a customer is staring at.
 */
export function progressSteps(currentStatus: string): ProgressStep[] {
  const currentIndex = STEPS.findIndex((step) => step.status === currentStatus);

  return STEPS.map((step, index) => ({
    status: step.status,
    label: step.label,
    state:
      currentIndex === -1
        ? "upcoming"
        : index < currentIndex
          ? "done"
          : index === currentIndex
            ? "current"
            : "upcoming",
  }));
}

export function headlineFor(status: string): string {
  return HEADLINES[status] ?? "Order in progress";
}

export function noteFor(status: string): string {
  return NOTES[status] ?? "We are keeping this page up to date as your order moves along.";
}

export function isComplete(status: string): boolean {
  return status === "Delivered";
}
