using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TwitchLib.Api;

namespace TwitchAutoGameSwitcher
{
    public class OAuthInfo
    {
        public required string AccessToken { get; set; }
        public required string UserLogin { get; set; }
        public required string BroadcasterId { get; set; }
    }

    public static class OAuthHelper
    {
        public const string OAuthFile = "twitch_oauth.dat";
        public const string ClientId = "h8g2xb4ce47gt5rhelstl7c1w0p55x";
        public const string RedirectUri = "http://localhost";
        public const string Scope = "channel:manage:broadcast";

        public static OAuthInfo? LoadOAuthInfo()
        {
            if (!File.Exists(OAuthFile)) return null;
            try
            {
                var encrypted = File.ReadAllBytes(OAuthFile);
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                return JsonSerializer.Deserialize<OAuthInfo>(json);
            }
            catch { return null; }
        }

        public static void SaveOAuthInfo(OAuthInfo info)
        {
            var json = JsonSerializer.Serialize(info);
            var data = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(OAuthFile, encrypted);
        }

        public static async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                var api = new TwitchAPI();
                api.Settings.ClientId = ClientId;
                var response = await  api.Auth.ValidateAccessTokenAsync(accessToken);
                return response != null;
            }
            catch { return false; }
        }

        public static TwitchAPI GetTwitchAPIClient()
        {
            var api = new TwitchAPI();
            api.Settings.ClientId = ClientId;
            var oauthInfo = LoadOAuthInfo();
            if (oauthInfo != null)
            {
                api.Settings.AccessToken = oauthInfo.AccessToken;
            }
            return api;
        }

        public static string GetOAuthUrl()
        {
            return $"https://id.twitch.tv/oauth2/authorize?client_id={ClientId}&redirect_uri={RedirectUri}&response_type=token&scope={Scope}";
        }
    }
}