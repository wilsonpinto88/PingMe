namespace PingMe.Api.Contracts.Locations;

public record LocationDto(Guid Id, string Name, Guid? ParentLocationId);
