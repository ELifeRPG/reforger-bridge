using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;
using ELifeRPG.Bridge.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace ELifeRPG.Bridge.Api.Authentication;

public sealed record PlayerToken(string AccessToken, string Jti, int ExpiresInSeconds);

/// <summary>
/// Owns the Bridge's own Client Credentials token (cached, refreshed before expiry) and performs
/// the player-impersonating token exchange directly against Keycloak — never through the Central API,
/// since Keycloak requires the exchange to be authenticated by the same client the subject token was
/// issued to (see ARCHITECTURE.md §4.3).
///
/// ExchangeForPlayerTokenAsync requires the caller's already-known session status and only exchanges
/// for "active" — a blocked or not-whitelisted account gets no token. This check exists here, not in
/// the caller, because Keycloak's own token-exchange grant does not enforce account status
/// (verified — see ARCHITECTURE.md §4.3): a disabled user's exchange still succeeds at the Keycloak
/// layer. Putting the gate inside this method means any future caller that wants a player token goes
/// through this same check automatically, instead of every call site needing to remember to check
/// status first.
/// </summary>
public sealed class BridgeTokenProvider(HttpClient httpClient, IOptions<KeycloakOptions> options) : IAccessTokenProvider
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private readonly KeycloakOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private (string Token, DateTimeOffset ExpiresAt)? _cached;

    public AllowedHostsValidator AllowedHostsValidator { get; } = new();

    public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        => await GetOwnTokenAsync(cancellationToken);

    public async Task<string> GetOwnTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is { } cached && cached.ExpiresAt > DateTimeOffset.UtcNow + RefreshMargin)
        {
            return cached.Token;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } stillCached && stillCached.ExpiresAt > DateTimeOffset.UtcNow + RefreshMargin)
            {
                return stillCached.Token;
            }

            var token = await RequestTokenAsync(
                [
                    new("client_id", _options.ClientId),
                    new("client_secret", _options.ClientSecret),
                    new("grant_type", "client_credentials"),
                ],
                cancellationToken);

            _cached = (token.AccessToken, DateTimeOffset.UtcNow.AddSeconds(token.ExpiresInSeconds));
            return token.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PlayerToken?> ExchangeForPlayerTokenAsync(string keycloakUsername, string status, CancellationToken cancellationToken = default)
    {
        if (status != "active")
        {
            return null;
        }

        var ownToken = await GetOwnTokenAsync(cancellationToken);

        var token = await RequestTokenAsync(
            [
                new("client_id", _options.ClientId),
                new("client_secret", _options.ClientSecret),
                new("grant_type", "urn:ietf:params:oauth:grant-type:token-exchange"),
                new("subject_token", ownToken),
                new("subject_token_type", "urn:ietf:params:oauth:token-type:access_token"),
                new("requested_subject", keycloakUsername),
            ],
            cancellationToken);

        var jti = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken).Id;

        return new PlayerToken(token.AccessToken, jti, token.ExpiresInSeconds);
    }

    private async Task<KeycloakTokenResponse> RequestTokenAsync(KeyValuePair<string, string>[] form, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync(
            $"realms/{_options.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(form),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: cancellationToken);
        return token ?? throw new InvalidOperationException("Keycloak did not return a token response.");
    }

    private sealed record KeycloakTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresInSeconds);
}
