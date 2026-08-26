export interface CustomerProduct {
  id: string;
  name: string;
  price: number;
}

export interface CustomerCategory {
  id: string;
  name: string;
  sortOrder: number;
  products: CustomerProduct[];
}

export interface CustomerMenu {
  id: string;
  name: string;
  categories: CustomerCategory[];
}

export interface ResolveQrCodeResponse {
  sessionId: string;
  venueName: string;
  locationLabel: string;
  menus: CustomerMenu[];
}

export interface CreateOrderItemRequest {
  productId: string;
  quantity: number;
}

export interface CreateOrderResponse {
  orderId: string;
  status: string;
}

export interface OrderStatusResponse {
  orderId: string;
  status: string;
}
