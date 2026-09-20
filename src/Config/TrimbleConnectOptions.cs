namespace TrimbleConnector.Config;

public sealed class TrimbleConnectOptions
{
    public const string SectionName = "TrimbleConnect";

    public const string DefaultEuApiBaseUrl = "https://app21.connect.trimble.com/tc/api/2.1";

    public const string DefaultEuApiBaseUrlV20 = "https://app21.connect.trimble.com/tc/api/2.0";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public string Scope { get; set; } = "openid trimble-connector-sync";

    public string AuthUrl { get; set; } = "https://id.trimble.com/oauth/authorize";

    public string TokenUrl { get; set; } = "https://id.trimble.com/oauth/token";

    public string TokenEndpoint { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = DefaultEuApiBaseUrl;

    public string RedirectUri { get; set; } = "http://localhost:5000/callback";

    public string EffectiveTokenUrl =>
        !string.IsNullOrWhiteSpace(TokenUrl) ? TokenUrl
        : !string.IsNullOrWhiteSpace(TokenEndpoint) ? TokenEndpoint
        : "https://id.trimble.com/oauth/token";

    public string EffectiveApiBaseUrl =>
        string.IsNullOrWhiteSpace(ApiBaseUrl) ? DefaultEuApiBaseUrl : ApiBaseUrl.TrimEnd('/');

    /// <summary>
    /// Object Sync, user profile, and file transfer live on Core REST 2.0.
    /// </summary>
    public string EffectiveApiBaseUrlV20
    {
        get
        {
            var v21 = EffectiveApiBaseUrl;
            if (v21.Contains("/api/2.1", StringComparison.Ordinal))
            {
                return v21.Replace("/api/2.1", "/api/2.0", StringComparison.Ordinal);
            }

            if (v21.EndsWith("/2.1", StringComparison.Ordinal))
            {
                return string.Concat(v21.AsSpan(0, v21.Length - 3), "2.0");
            }

            if (v21.Contains("/api/2.0", StringComparison.Ordinal) || v21.EndsWith("/2.0", StringComparison.Ordinal))
            {
                return v21;
            }

            return DefaultEuApiBaseUrlV20;
        }
    }
}
