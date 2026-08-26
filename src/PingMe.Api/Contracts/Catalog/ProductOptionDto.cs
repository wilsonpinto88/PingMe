namespace PingMe.Api.Contracts.Catalog;

public record ProductOptionDto(Guid Id, Guid ProductId, string Name, decimal PriceDelta);
