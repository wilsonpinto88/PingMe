namespace PingMe.Api.Contracts.Ordering;

public record CustomerMenuDto(Guid Id, string Name, List<CustomerCategoryDto> Categories);
