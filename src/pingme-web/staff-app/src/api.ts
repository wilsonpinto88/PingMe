import type { AdminOrderDto, AuthResponse } from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

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
