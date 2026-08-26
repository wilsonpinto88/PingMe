namespace PingMe.Api.Contracts.Ordering;

public record CustomerCategoryDto(Guid Id, string Name, int SortOrder, List<CustomerProductDto> Products);
