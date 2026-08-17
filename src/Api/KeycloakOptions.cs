namespace ELifeRPG.Bridge.Api;

public sealed class KeycloakOptions
{
    public required string BaseUrl { get; init; }

    public required string Realm { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }
}
