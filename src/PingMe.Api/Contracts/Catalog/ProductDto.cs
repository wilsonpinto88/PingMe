namespace PingMe.Api.Contracts.Catalog;

public record ProductDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    decimal Price,
    bool IsAvailable,
    string? Description,
    string? ImageUrl);
