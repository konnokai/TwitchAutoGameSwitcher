using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
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
        public const string RedirectUri = "http://localhost:9999/";
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

        public static async Task<bool> ValidateTokenAsync(string accessToken)
        {
            try
            {
                var api = GetTwitchAPIClient();
                var response = await api.Auth.ValidateAccessTokenAsync(accessToken);
                return response != null;
            }
            catch { return false; }
        }

        // 保留原有 API 兼容性
        public static Task<bool> StartOAuthAsync() => StartOAuthAsync(CancellationToken.None);

        public static async Task<bool> StartOAuthAsync(CancellationToken cancellationToken)
        {
            bool result = false;

            // 產生隨機 state 參數
            string state = Guid.NewGuid().ToString("N");
            string oauthUrl = $"https://id.twitch.tv/oauth2/authorize" +
                $"?response_type=token" +
                $"&client_id={ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                $"&scope={Scope}" +
                $"&force_verify=true" +
                $"&state={state}";

            // 啟動 TcpListener 等待 Twitch redirect
            var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();

            // 用預設瀏覽器開啟授權頁
            Process.Start(new ProcessStartInfo(oauthUrl) { UseShellExecute = true });

            try
            {
                string accessToken = string.Empty;
                string receivedState = string.Empty;
                string error = string.Empty;
                string errorDescription = string.Empty;

                // 只處理帶有 state 的請求
                var context = await AwaitOAuthCallbackAsync(listener, cancellationToken);

                // 直接從 QueryString 取得參數
                var query = context.Request.QueryString;
                accessToken = query["access_token"] ?? string.Empty;
                receivedState = query["state"] ?? string.Empty;
                error = query["error"] ?? string.Empty;
                errorDescription = query["error_description"] ?? string.Empty;
                if (!string.IsNullOrEmpty(errorDescription))
                {
                    errorDescription = System.Web.HttpUtility.UrlDecode(errorDescription);
                }

                string responseHtml = string.Empty;
                if (!string.IsNullOrEmpty(error))
                {
                    responseHtml = $"<html><body><h2>授權失敗</h2><p>錯誤: {error}</p><p>{errorDescription}</p></body></html>";
                }
                else if (!string.IsNullOrEmpty(accessToken) && receivedState == state)
                {
                    var api = GetTwitchAPIClient();
                    var response = await api.Auth.ValidateAccessTokenAsync(accessToken);
                    var info = new OAuthInfo
                    {
                        AccessToken = accessToken,
                        UserLogin = response.Login,
                        BroadcasterId = response.UserId
                    };
                    SaveOAuthInfo(info);

                    responseHtml = "<html><body><h2>授權成功，請關閉此頁面</h2>";
                    result = true;
                }
                else if (!string.IsNullOrEmpty(accessToken) && receivedState != state)
                {
                    responseHtml = "<html><body><h2>授權失敗，state 驗證失敗，請重試</h2></body></html>";
                }
                else
                {
                    responseHtml = "<html><body><h2>授權失敗，請重試</h2></body></html>";
                }

                SendHtmlResponse(context, responseHtml);
            }
            catch (OperationCanceledException)
            {
                // 取消時不彈出錯誤
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OAuth 流程發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                listener.Stop();
            }

            return result;
        }

        // 統一處理 HTML 回應，設定正確 Content-Type 與編碼
        private static void SendHtmlResponse(HttpListenerContext context, string html)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        // 等待帶有 access_token、error 或 state 參數的 OAuth callback
        private static async Task<HttpListenerContext> AwaitOAuthCallbackAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            HttpListenerContext context;
            bool hasState;

            do
            {
                // 支援取消
                var getContextTask = listener.GetContextAsync();

                var completedTask = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, cancellationToken));
                if (completedTask != getContextTask)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                context = getContextTask.Result;
                var query = context.Request.QueryString;
                hasState = !string.IsNullOrEmpty(query["state"]);

                if (!hasState)
                {
                    // 回傳 JS 將 fragment 轉為 query string 並重新導向
                    string js = @"<html><body><script>
                        if (window.location.hash.length > 1) {
                            var q = window.location.hash.substring(1);
                            window.location.href = window.location.pathname + '?' + q;
                        } else {
                            document.write('請從 Twitch OAuth 頁面登入');
                        }
                        </script></body></html>";
                    SendHtmlResponse(context, js);
                }
            }
            while (!hasState);

            return context;
        }
    }
}