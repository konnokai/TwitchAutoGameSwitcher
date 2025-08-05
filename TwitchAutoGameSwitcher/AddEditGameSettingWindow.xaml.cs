using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwitchLib.Api;

namespace TwitchAutoGameSwitcher
{
    public partial class AddEditGameSettingWindow : Window
    {
        public GameSetting GameSetting { get; private set; }
        private readonly TwitchAPI _twitchApi;
        private const string BoxArtCacheDir = "BoxArtCache";

        public AddEditGameSettingWindow()
        {
            InitializeComponent();
            OkButton.Click += OkButton_Click;
            CancelButton.Click += (s, e) => DialogResult = false;
            BrowseExeButton.Click += BrowseExeButton_Click;
            SearchGameButton.Click += SearchGameButton_Click;
            DetectExeButton.Click += DetectExeButton_Click;
            _twitchApi = OAuthHelper.GetTwitchAPIClient();
        }

        private async void SearchGameButton_Click(object sender, RoutedEventArgs e)
        {
            var query = GameNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;
            var encodedQuery = Uri.EscapeDataString(query);
            try
            {
                var result = await _twitchApi.Helix.Search.SearchCategoriesAsync(encodedQuery);
                if (result.Games != null && result.Games.Length > 0)
                {
                    dynamic selectedGame = null;
                    if (result.Games.Length == 1)
                    {
                        selectedGame = result.Games[0];
                    }
                    else
                    {
                        // �h�����G�A�u�X��ܵ���
                        var dialog = new Window
                        {
                            Title = "��ܹC������",
                            Width = 500,
                            Height = 400,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this
                        };
                        var listBox = new ListBox { Margin = new Thickness(10), MaxHeight = 300 };
                        foreach (var g in result.Games)
                        {
                            listBox.Items.Add(new ListBoxItem
                            {
                                Content = $"{g.Name} (ID: {g.Id})",
                                Tag = g
                            });
                        }
                        listBox.SelectedIndex = 0;
                        var okBtn = new Button { Content = "�T�w", Width = 80, Margin = new Thickness(10) };
                        okBtn.Click += (s, e2) => { dialog.DialogResult = true; };
                        var panel = new StackPanel();
                        panel.Children.Add(listBox);
                        panel.Children.Add(okBtn);
                        dialog.Content = panel;
                        if (dialog.ShowDialog() == true && listBox.SelectedItem is ListBoxItem item)
                        {
                            selectedGame = item.Tag;
                        }
                        else
                        {
                            return; // �ϥΪ̥����
                        }
                    }
                    if (selectedGame != null)
                    {
                        GameIdTextBox.Text = selectedGame.Id;
                        GameNameTextBox.Text = selectedGame.Name;
                        var boxArtUrl = selectedGame.BoxArtUrl.Replace("{width}", "52").Replace("{height}", "72");
                        var localPath = await DownloadBoxArtAsync(boxArtUrl, selectedGame.Id);
                        BoxArtImage.Source = new BitmapImage(new Uri(localPath));
                    }
                }
                else
                {
                    MessageBox.Show("�d�L�C�������C", "����", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"�d�ߥ���: {ex.Message}", "���~", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> DownloadBoxArtAsync(string url, string gameId)
        {
            if (!Directory.Exists(BoxArtCacheDir)) Directory.CreateDirectory(BoxArtCacheDir);
            var localPath = Path.Combine(BoxArtCacheDir, $"{gameId}.jpg");
            if (!File.Exists(localPath))
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, bytes);
            }
            return Path.GetFullPath(localPath);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(PriorityTextBox.Text, out int priority) &&
                !string.IsNullOrWhiteSpace(GameNameTextBox.Text) &&
                !string.IsNullOrWhiteSpace(GameIdTextBox.Text) &&
                !string.IsNullOrWhiteSpace(ExecutableTextBox.Text))
            {
                GameSetting = new GameSetting
                {
                    Priority = priority,
                    Name = GameNameTextBox.Text,
                    Id = GameIdTextBox.Text,
                    BoxArtPath = BoxArtImage.Source is BitmapImage bmp ? bmp.UriSource?.LocalPath : null,
                    ExecutableName = ExecutableTextBox.Text
                };
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("�Ч����g�Ҧ����C", "���~", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseExeButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "������ (*.exe)|*.exe",
                Title = "��ܹC��������"
            };
            if (dlg.ShowDialog() == true)
            {
                ExecutableTextBox.Text = Path.GetFileName(dlg.FileName);
            }
        }

        private void DetectExeButton_Click(object sender, RoutedEventArgs e)
        {
            var windowList = new List<WindowInfo>();
            WindowHelper.FillWindowList(windowList, WindowSearchMode.ExcludeMinimized);
            if (windowList.Count == 0)
            {
                MessageBox.Show("�ثe�S�������������椤�������C", "����", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // ��ܿ�ܵ����AListBox �[�J MaxHeight �ýT�O�i�u��
            var dialog = new Window
            {
                Title = "��ܰ��椤�{��",
                Width = 800,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var listBox = new ListBox { ItemsSource = windowList, Margin = new Thickness(10), MaxHeight = 300 };
            var okBtn = new Button { Content = "�T�w", Width = 80, Margin = new Thickness(10) };
            okBtn.Click += (s, e2) => { dialog.DialogResult = true; };
            var panel = new StackPanel();
            panel.Children.Add(listBox);
            panel.Children.Add(okBtn);
            dialog.Content = panel;

            if (dialog.ShowDialog() == true && listBox.SelectedItem is WindowInfo selectedWindow)
            {
                ExecutableTextBox.Text = selectedWindow.Executable;
            }
        }
    }
}
