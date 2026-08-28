namespace PingMe.Application.Ordering;

public record AdminOrderDto(Guid Id, string Status, DateTime CreatedAt, List<AdminOrderItemDto> Items, string PosDeliveryStatus);
