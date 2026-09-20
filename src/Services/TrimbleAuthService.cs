using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrimbleConnector.Config;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

public interface ITrimbleAuthService
{
    bool HasRefreshToken { get; }

    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);

    void StoreRefreshToken(string refreshToken, string? accessToken = null, int expiresIn = 0);
}

/// <summary>
/// Refreshes Trimble Identity access tokens from a stored refresh token and caches
/// the current access token in memory. Trimble refresh tokens are single-use, so a
/// newly issued refresh token is persisted next to the service.
/// </summary>
public sealed class TrimbleAuthService : ITrimbleAuthService
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(90);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<TrimbleConnectOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TrimbleAuthService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private string? _refreshToken;

    public TrimbleAuthService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<TrimbleConnectOptions> options,
        IHostEnvironment environment,
        ILogger<TrimbleAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _environment = environment;
        _logger = logger;
        _refreshToken = LoadPersistedRefreshToken() ?? options.CurrentValue.RefreshToken;
    }

    public bool HasRefreshToken => !string.IsNullOrWhiteSpace(_refreshToken);

    public void StoreRefreshToken(string refreshToken, string? accessToken = null, int expiresIn = 0)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
        }

        _refreshToken = refreshToken.Trim();
        PersistRefreshToken(_refreshToken);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            _accessToken = accessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn > 0 ? expiresIn : 3600);
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (HasValidAccessToken)
        {
            return _accessToken!;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (HasValidAccessToken)
            {
                return _accessToken!;
            }

            await RefreshAsync(cancellationToken).ConfigureAwait(false);
            return _accessToken ?? throw new InvalidOperationException("Token refresh succeeded without an access token.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool HasValidAccessToken =>
        !string.IsNullOrWhiteSpace(_accessToken) && DateTimeOffset.UtcNow + RefreshSkew < _expiresAt;

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException("TrimbleConnect:ClientId and ClientSecret must be configured.");
        }

        var refreshToken = _refreshToken ?? options.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("TrimbleConnect:RefreshToken is missing. Complete an OAuth authorization-code flow and store the refresh token.");
        }

        var tokenEndpoint = options.EffectiveTokenUrl;

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.ClientId
        });

        var client = _httpClientFactory.CreateClient("TrimbleIdentity");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Trimble Identity token refresh failed with {StatusCode}.",
                (int)response.StatusCode);
            throw new HttpRequestException($"Token refresh failed with HTTP {(int)response.StatusCode}.");
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(payload, JsonDefaults.Serializer)
            ?? throw new InvalidOperationException("Trimble Identity returned an empty token payload.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new InvalidOperationException("Trimble Identity returned no access_token.");
        }

        _accessToken = token.AccessToken;
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600);

        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            _refreshToken = token.RefreshToken;
            PersistRefreshToken(token.RefreshToken);
        }

        _logger.LogInformation(
            "Refreshed Trimble Identity access token. Expires around {ExpiresAt:u}.",
            _expiresAt);
    }

    private string RefreshTokenPath =>
        Path.Combine(_environment.ContentRootPath, "data", "refresh.token");

    private string? LoadPersistedRefreshToken()
    {
        try
        {
            var path = RefreshTokenPath;
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read persisted refresh token.");
            return null;
        }
    }

    private void PersistRefreshToken(string refreshToken)
    {
        try
        {
            var path = RefreshTokenPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, refreshToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist rotated refresh token. The next restart may require a new token.");
        }
    }
}
