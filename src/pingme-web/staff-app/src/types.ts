export interface AuthResponse {
  token: string;
}

export interface AdminOrderItemDto {
  productName: string;
  unitPrice: number;
  quantity: number;
}

export interface AdminOrderDto {
  id: string;
  status: string;
  createdAt: string;
  items: AdminOrderItemDto[];
  /** Where the order must be taken. Resolved from the scanned QR code. */
  locationLabel: string;
  /** Whether the order reached the venue POS or ERP system. */
  posDeliveryStatus: string;
}

export type ConnectionState = "connecting" | "connected" | "reconnecting" | "disconnected";
