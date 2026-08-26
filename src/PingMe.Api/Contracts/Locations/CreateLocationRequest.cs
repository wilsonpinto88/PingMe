namespace PingMe.Api.Contracts.Locations;

public record CreateLocationRequest(string Name, Guid? ParentLocationId);
