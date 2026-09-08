import { describe, expect, it } from "vitest";
import { headlineFor, isComplete, progressSteps } from "./orderProgress";

describe("progressSteps", () => {
  it("marks earlier steps done and later steps upcoming", () => {
    const steps = progressSteps("Preparing");

    expect(steps.map((step) => step.state)).toEqual([
      "done",
      "done",
      "current",
      "upcoming",
      "upcoming",
    ]);
  });

  it("marks the first step current for a brand new order", () => {
    expect(progressSteps("Received")[0].state).toBe("current");
  });

  it("marks everything done or current once delivered", () => {
    const steps = progressSteps("Delivered");

    expect(steps[steps.length - 1].state).toBe("current");
    expect(steps.slice(0, -1).every((step) => step.state === "done")).toBe(true);
  });

  it("leaves every step upcoming for a status it does not know", () => {
    const steps = progressSteps("Refunded");

    expect(steps.every((step) => step.state === "upcoming")).toBe(true);
  });
});

describe("headlineFor", () => {
  it("uses customer wording rather than the raw status name", () => {
    expect(headlineFor("Accepted")).not.toContain("Accepted");
  });

  it("falls back to a neutral headline for an unknown status", () => {
    expect(headlineFor("Refunded")).toBe("Order in progress");
  });
});

describe("isComplete", () => {
  it("is only true once the order is delivered", () => {
    expect(isComplete("Delivered")).toBe(true);
    expect(isComplete("Ready")).toBe(false);
  });
});
