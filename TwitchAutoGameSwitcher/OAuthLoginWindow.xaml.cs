using System;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using System.Text.RegularExpressions;

namespace TwitchAutoGameSwitcher
{
    public partial class OAuthLoginWindow : Window
    {
        public OAuthLoginWindow()
        {
            InitializeComponent();
            Loaded += OAuthLoginWindow_Loaded;
        }

        private void OAuthLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.Source = new Uri(OAuthHelper.GetOAuthUrl());
        }

        private async void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            var url = WebView.Source.ToString();
            if (url.StartsWith(OAuthHelper.RedirectUri) && url.Contains("access_token"))
            {
                try
                {
                    var match = Regex.Match(url, "access_token=([^&]+)");
                    if (match.Success)
                    {
                        var accessToken = match.Groups[1].Value;
                        var api = OAuthHelper.GetTwitchAPIClient();
                        var response = await api.Auth.ValidateAccessTokenAsync(accessToken);
                        if (response != null)
                        {
                            var info = new OAuthInfo
                            {
                                AccessToken = accessToken,
                                UserLogin = response.Login,
                                BroadcasterId = response.UserId
                            };
                            OAuthHelper.SaveOAuthInfo(info);
                            //MessageBox.Show("Twitch 登入成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            DialogResult = true;
                        }
                        else
                        {
                            MessageBox.Show("無法驗證 AccessToken，請重新登入。", "登入失敗", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("無法解析 AccessToken，請重新登入。", "登入失敗", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"登入過程發生錯誤：{ex.Message}\n請檢查網路連線或稍後再試。", "登入失敗", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Close();
            }
        }
    }
}
