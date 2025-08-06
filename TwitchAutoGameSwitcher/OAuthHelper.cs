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

        // �O�d�즳 API �ݮe��
        public static Task<bool> StartOAuthAsync() => StartOAuthAsync(CancellationToken.None);

        public static async Task<bool> StartOAuthAsync(CancellationToken cancellationToken)
        {
            bool result = false;

            // �����H�� state �Ѽ�
            string state = Guid.NewGuid().ToString("N");
            string oauthUrl = $"https://id.twitch.tv/oauth2/authorize" +
                $"?response_type=token" +
                $"&client_id={ClientId}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                $"&scope={Scope}" +
                $"&force_verify=true" +
                $"&state={state}";

            // �Ұ� TcpListener ���� Twitch redirect
            var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();

            // �ιw�]�s�����}�ұ��v��
            Process.Start(new ProcessStartInfo(oauthUrl) { UseShellExecute = true });

            try
            {
                string accessToken = string.Empty;
                string receivedState = string.Empty;
                string error = string.Empty;
                string errorDescription = string.Empty;

                // �u�B�z�a�� state ���ШD
                var context = await AwaitOAuthCallbackAsync(listener, cancellationToken);

                // �����q QueryString ���o�Ѽ�
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
                    responseHtml = $"<html><body><h2>���v����</h2><p>���~: {error}</p><p>{errorDescription}</p></body></html>";
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

                    responseHtml = "<html><body><h2>���v���\�A������������</h2>";
                    result = true;
                }
                else if (!string.IsNullOrEmpty(accessToken) && receivedState != state)
                {
                    responseHtml = "<html><body><h2>���v���ѡAstate ���ҥ��ѡA�Э���</h2></body></html>";
                }
                else
                {
                    responseHtml = "<html><body><h2>���v���ѡA�Э���</h2></body></html>";
                }

                SendHtmlResponse(context, responseHtml);
            }
            catch (OperationCanceledException)
            {
                // �����ɤ��u�X���~
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OAuth �y�{�o�Ϳ��~: {ex.Message}", "���~", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                listener.Stop();
            }

            return result;
        }

        // �Τ@�B�z HTML �^���A�]�w���T Content-Type �P�s�X
        private static void SendHtmlResponse(HttpListenerContext context, string html)
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }

        // ���ݱa�� access_token�Berror �� state �Ѽƪ� OAuth callback
        private static async Task<HttpListenerContext> AwaitOAuthCallbackAsync(HttpListener listener, CancellationToken cancellationToken)
        {
            HttpListenerContext context;
            bool hasState;

            do
            {
                // �䴩����
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
                    // �^�� JS �N fragment �ର query string �í��s�ɦV
                    string js = @"<html><body><script>
                        if (window.location.hash.length > 1) {
                            var q = window.location.hash.substring(1);
                            window.location.href = window.location.pathname + '?' + q;
                        } else {
                            document.write('�бq Twitch OAuth �����n�J');
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