namespace PingMe.Api.Contracts.Catalog;

public record CategoryDto(Guid Id, Guid MenuId, string Name, int SortOrder);
