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
                MessageBox.Show("�L�k���� OAuth �y�{�A�нT�{�����έ��աC", "���~", MessageBoxButton.OK, MessageBoxImage.Error);
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
