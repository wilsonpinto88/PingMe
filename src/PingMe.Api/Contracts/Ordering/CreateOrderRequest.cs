namespace PingMe.Api.Contracts.Ordering;

public record CreateOrderRequest(Guid SessionId, List<CreateOrderItemRequest> Items);
