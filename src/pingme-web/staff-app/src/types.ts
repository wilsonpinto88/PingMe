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
}
