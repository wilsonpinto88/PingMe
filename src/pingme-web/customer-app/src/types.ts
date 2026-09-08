export type VenueThemeMode = "Light" | "Dark";

export interface VenueTheme {
  primaryColor: string;
  accentColor: string;
  currencyCode: string;
  themeMode: VenueThemeMode;
  logoUrl: string | null;
  heroImageUrl: string | null;
  tagline: string | null;
}

export interface CustomerProduct {
  id: string;
  name: string;
  price: number;
  description: string | null;
  imageUrl: string | null;
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
  theme: VenueTheme;
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
