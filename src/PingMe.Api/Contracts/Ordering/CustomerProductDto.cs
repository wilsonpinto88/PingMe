namespace PingMe.Api.Contracts.Ordering;

public record CustomerProductDto(
    Guid Id,
    string Name,
    decimal Price,
    string? Description,
    string? ImageUrl);
