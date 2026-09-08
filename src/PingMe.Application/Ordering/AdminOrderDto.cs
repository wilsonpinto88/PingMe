namespace PingMe.Application.Ordering;

public record AdminOrderDto(
    Guid Id,
    string Status,
    DateTime CreatedAt,
    List<AdminOrderItemDto> Items,
    string PosDeliveryStatus,
    /// <summary>Where staff must take this order. Without it the dashboard is not actionable.</summary>
    string LocationLabel);
