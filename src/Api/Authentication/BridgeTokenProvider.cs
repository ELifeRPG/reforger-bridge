using System.Text.Json.Serialization;
using ELifeRPG.Bridge.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace ELifeRPG.Bridge.Api.Authentication;

/// <summary>
/// Owns the Bridge's own Client Credentials token, cached and refreshed before expiry, used to
/// authenticate every Bridge → Central API call.
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
