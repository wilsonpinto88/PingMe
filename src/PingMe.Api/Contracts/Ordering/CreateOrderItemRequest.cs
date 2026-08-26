namespace PingMe.Api.Contracts.Ordering;

public record CreateOrderItemRequest(Guid ProductId, int Quantity);
