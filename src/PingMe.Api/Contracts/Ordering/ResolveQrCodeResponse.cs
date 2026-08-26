namespace PingMe.Api.Contracts.Ordering;

public record ResolveQrCodeResponse(Guid SessionId, string VenueName, string LocationLabel, List<CustomerMenuDto> Menus);
