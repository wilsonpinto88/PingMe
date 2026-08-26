namespace PingMe.Api.Contracts.Ordering;

public record OrderStatusResponse(Guid OrderId, string Status);
