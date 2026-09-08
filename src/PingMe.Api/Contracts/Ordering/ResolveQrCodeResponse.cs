namespace PingMe.Api.Contracts.Ordering;

using PingMe.Api.Contracts.Venues;

public record ResolveQrCodeResponse(
    Guid SessionId,
    string VenueName,
    string LocationLabel,
    VenueThemeDto Theme,
    List<CustomerMenuDto> Menus);
