using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrimbleConnector.Config;
using TrimbleConnector.Models;

namespace TrimbleConnector.Services;

/// <summary>
/// Builds the Trimble Identity authorize URL and exchanges the callback code
/// for tokens. The ASP.NET /callback route hosts the redirect (port 5000).
/// </summary>
public sealed class AuthSetupHelper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITrimbleAuthService _auth;
    private readonly IOptionsMonitor<TrimbleConnectOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthSetupHelper> _logger;

    public AuthSetupHelper(
        IHttpClientFactory httpClientFactory,
        ITrimbleAuthService auth,
        IOptionsMonitor<TrimbleConnectOptions> options,
        IHostEnvironment environment,
        ILogger<AuthSetupHelper> logger)
    {
        _httpClientFactory = httpClientFactory;
        _auth = auth;
        _options = options;
        _environment = environment;
        _logger = logger;
        EnsurePersistedTokenLoaded();
    }

    public bool HasRefreshToken
    {
        get
        {
            EnsurePersistedTokenLoaded();
            return _auth.HasRefreshToken;
        }
    }

    public string BuildAuthorizeUrl()
    {
        var options = _options.CurrentValue;
        var authUrl = string.IsNullOrWhiteSpace(options.AuthUrl)
            ? "https://id.trimble.com/oauth/authorize"
            : options.AuthUrl;
        var scope = string.IsNullOrWhiteSpace(options.Scope) ? "openid trimble-connector-sync" : options.Scope;
        var redirect = string.IsNullOrWhiteSpace(options.RedirectUri)
            ? "http://localhost:5000/callback"
            : options.RedirectUri;

        return authUrl
            + "?response_type=code"
            + "&client_id=" + Uri.EscapeDataString(options.ClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(redirect)
            + "&scope=" + Uri.EscapeDataString(scope);
    }

    public async Task CompleteAuthorizationAsync(string code, CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var token = await ExchangeAuthorizationCodeAsync(options, code, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            throw new InvalidOperationException("Trimble Identity did not return a refresh_token.");
        }

        _auth.StoreRefreshToken(token.RefreshToken, token.AccessToken, token.ExpiresIn);
        _logger.LogInformation("Stored Trimble Identity refresh token at {Path}.", RefreshTokenPath);
    }

    private void EnsurePersistedTokenLoaded()
    {
        if (_auth.HasRefreshToken)
        {
            return;
        }

        var fromSettings = _options.CurrentValue.RefreshToken;
        if (!string.IsNullOrWhiteSpace(fromSettings))
        {
            _auth.StoreRefreshToken(fromSettings.Trim());
            return;
        }

        var path = RefreshTokenPath;
        if (!File.Exists(path))
        {
            return;
        }

        var persisted = File.ReadAllText(path).Trim();
        if (!string.IsNullOrWhiteSpace(persisted))
        {
            _auth.StoreRefreshToken(persisted);
        }
    }

    private string RefreshTokenPath =>
        Path.Combine(_environment.ContentRootPath, "data", "refresh.token");

    private async Task<TokenResponse> ExchangeAuthorizationCodeAsync(
        TrimbleConnectOptions options,
        string code,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.EffectiveTokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri
        });

        var client = _httpClientFactory.CreateClient("TrimbleIdentity");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Authorization-code exchange failed with HTTP {StatusCode}.", (int)response.StatusCode);
            throw new HttpRequestException($"Authorization-code exchange failed with HTTP {(int)response.StatusCode}.");
        }

        return JsonSerializer.Deserialize<TokenResponse>(payload, JsonDefaults.Serializer)
            ?? throw new InvalidOperationException("Trimble Identity returned an empty token payload.");
    }
}
