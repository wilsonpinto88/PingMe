namespace PingMe.Api.Contracts.Locations;

public record QrCodeDto(Guid Id, Guid LocationId, string Code);
