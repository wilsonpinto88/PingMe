import type {
  CreateOrderItemRequest,
  CreateOrderResponse,
  OrderStatusResponse,
  ResolveQrCodeResponse,
} from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5190";

export async function resolveQrCode(code: string): Promise<ResolveQrCodeResponse> {
  const response = await fetch(`${API_BASE_URL}/p/${encodeURIComponent(code)}`);
  if (!response.ok) {
    throw new Error(`Failed to resolve QR code: ${response.status}`);
  }
  return response.json();
}

export async function placeOrder(
  sessionId: string,
  items: CreateOrderItemRequest[],
): Promise<CreateOrderResponse> {
  const response = await fetch(`${API_BASE_URL}/orders`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ sessionId, items }),
  });
  if (!response.ok) {
    throw new Error(`Failed to place order: ${response.status}`);
  }
  return response.json();
}

export async function getOrderStatus(
  orderId: string,
  sessionId: string,
): Promise<OrderStatusResponse> {
  const response = await fetch(
    `${API_BASE_URL}/orders/${orderId}/status?sessionId=${sessionId}`,
  );
  if (!response.ok) {
    throw new Error(`Failed to fetch order status: ${response.status}`);
  }
  return response.json();
}
