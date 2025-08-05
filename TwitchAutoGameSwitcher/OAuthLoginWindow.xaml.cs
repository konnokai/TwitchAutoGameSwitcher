using System.Windows;

namespace TwitchAutoGameSwitcher
{
    public partial class OAuthLoginWindow : Window
    {
        private CancellationTokenSource? _cts;
        public OAuthLoginWindow()
        {
            InitializeComponent();
            Loaded += OAuthLoginWindow_Loaded;
            Closed += OAuthLoginWindow_Closed;
        }

        private async void OAuthLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            var result = await OAuthHelper.StartOAuthAsync(_cts.Token);
            if (result)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("無法完成 OAuth 流程，請確認網路或重試。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private void OAuthLoginWindow_Closed(object? sender, EventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
