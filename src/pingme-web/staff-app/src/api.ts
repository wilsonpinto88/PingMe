import type { AdminOrderDto, AuthResponse } from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

/** Carries the server's own message (e.g. "email already registered"), not just a status code. */
export class ApiError extends Error {}

export async function login(email: string, password: string): Promise<AuthResponse> {
  const response = await fetch(`${API_BASE_URL}/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) {
    throw new Error(`Login failed: ${response.status}`);
  }
  return response.json();
}

export async function registerTenant(
  tenantName: string,
  ownerEmail: string,
  ownerPassword: string,
): Promise<AuthResponse> {
  const response = await fetch(`${API_BASE_URL}/auth/register-tenant`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ tenantName, ownerEmail, ownerPassword }),
  });
  if (!response.ok) {
    throw new ApiError(await extractErrorMessage(response));
  }
  return response.json();
}

/**
 * The API returns three different shapes for a failed registration: a plain
 * string for a 409 (email taken), a string array for Identity password-rule
 * failures, and a validation-problem object for missing fields. This surfaces
 * whichever one came back instead of a generic "something went wrong".
 */
async function extractErrorMessage(response: Response): Promise<string> {
  try {
    const body = await response.json();
    if (typeof body === "string") {
      return body;
    }
    if (Array.isArray(body)) {
      return body.join(" ");
    }
    if (body && typeof body === "object" && "errors" in body) {
      const errors = (body as { errors: Record<string, string[]> }).errors;
      return Object.values(errors).flat().join(" ");
    }
  } catch {
    // Not JSON — fall through to the generic message below.
  }
  return `Registration failed (${response.status}). Please try again.`;
}

export async function getOrders(token: string): Promise<AdminOrderDto[]> {
  const response = await fetch(`${API_BASE_URL}/admin/orders`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!response.ok) {
    throw new Error(`Failed to fetch orders: ${response.status}`);
  }
  return response.json();
}

export async function updateOrderStatus(
  token: string,
  orderId: string,
  status: string,
): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/admin/orders/${orderId}/status`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ status }),
  });
  if (!response.ok) {
    throw new Error(`Failed to update order status: ${response.status}`);
  }
}
