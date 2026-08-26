namespace PingMe.Api.Contracts.Ordering;

public record AdminOrderDto(Guid Id, string Status, DateTime CreatedAt, List<AdminOrderItemDto> Items);
