using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Channels.ModifyChannelInformation;

namespace TwitchAutoGameSwitcher
{
    public class GameSetting
    {
        public int Priority { get; set; }
        public required string BoxArtPath { get; set; } // 本地快取路徑
        public required string Name { get; set; }
        public required string Id { get; set; }
        public required string ExecutableName { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<GameSetting> _gameSettings = new();
        private const string SettingsFile = "GameSettings.json";

        private System.Timers.Timer _scanTimer;
        private TwitchAPI _twitchApi;
        private OAuthInfo _oauthInfo;
        private string? _lastGameId = null;

        public MainWindow()
        {
            InitializeComponent();
            CheckOAuthAsync();
            LoadGameSettings();
            GameDataGrid.ItemsSource = _gameSettings;
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
            StartButton.Click += StartButton_Click;
            StopButton.Click += StopButton_Click;
            AddButton.Click += AddButton_Click;
        }

        private async void CheckOAuthAsync()
        {
            var oauth = OAuthHelper.LoadOAuthInfo();
            if (oauth == null || !await OAuthHelper.ValidateTokenAsync(oauth.AccessToken))
            {
                var loginWindow = new OAuthLoginWindow();
                if (loginWindow.ShowDialog() == true)
                {
                    oauth = OAuthHelper.LoadOAuthInfo();
                }
            }
            // TODO: 若 oauth 有效，進入主流程
        }

        private void LoadGameSettings()
        {
            if (File.Exists(SettingsFile))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFile);
                    var list = JsonSerializer.Deserialize<ObservableCollection<GameSetting>>(json);
                    if (list != null)
                    {
                        _gameSettings = new ObservableCollection<GameSetting>(list);
                    }
                }
                catch { /* TODO: log or show error */ }
            }
        }

        private void SaveGameSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_gameSettings);
                File.WriteAllText(SettingsFile, json);
            }
            catch { /* TODO: log or show error */ }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TODO: 切換語言資源
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _oauthInfo = OAuthHelper.LoadOAuthInfo();
            if (_oauthInfo == null)
            {
                StatusLab.Content = "尚未登入 Twitch。";
                MessageBox.Show("尚未登入 Twitch。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            _twitchApi = OAuthHelper.GetTwitchAPIClient();
            _scanTimer = new System.Timers.Timer(5000); // 每 5 秒掃描一次
            _scanTimer.Elapsed += ScanTimer_Elapsed;
            _scanTimer.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusLab.Content = "已開始自動切換。";
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            _scanTimer?.Stop();
            _scanTimer = null;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusLab.Content = "已停止自動切換。";
        }

        private async void ScanTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                var windowList = new List<WindowInfo>();
                WindowHelper.FillWindowList(windowList, WindowSearchMode.ExcludeMinimized);
                if (windowList.Count == 0)
                {
                    Dispatcher.Invoke(() => StatusLab.Content = "未偵測到遊戲視窗。");
                    return;
                }

                var matched = _gameSettings
                    .OrderByDescending(g => g.Priority)
                    .FirstOrDefault(g => windowList.Any(exe => exe.Executable.EndsWith(g.ExecutableName, StringComparison.OrdinalIgnoreCase)));

                if (matched != null && matched.Id != _lastGameId)
                {
                    await UpdateTwitchGameAsync(matched.Id, matched.Name);
                    _lastGameId = matched.Id;
                }
                else if (matched != null)
                {
                    Dispatcher.Invoke(() => StatusLab.Content = $"已偵測到：{matched.Name}，分類未變更。");
                }
                else
                {
                    Dispatcher.Invoke(() => StatusLab.Content = "未找到對應遊戲設定。");
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => StatusLab.Content = $"偵測過程發生錯誤：{ex.Message}");
            }
        }

        private async Task UpdateTwitchGameAsync(string gameId, string? gameName = null)
        {
            try
            {
                var req = new ModifyChannelInformationRequest { GameId = gameId };
                await _twitchApi.Helix.Channels.ModifyChannelInformationAsync(_oauthInfo.BroadcasterId, req);
                Dispatcher.Invoke(() => StatusLab.Content = $"已切換 Twitch 分類：{gameName ?? gameId}");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => StatusLab.Content = $"切換 Twitch 分類失敗：{ex.Message}");
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditGameSettingWindow();
            if (dialog.ShowDialog() == true)
            {
                var newSetting = dialog.GameSetting;
                if (newSetting != null)
                {
                    // 檢查 ExecutableName 是否重複
                    if (_gameSettings.Any(g => string.Equals(g.ExecutableName, newSetting.ExecutableName, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show($"執行檔名稱 '{newSetting.ExecutableName}' 已存在，請勿重複。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    _gameSettings.Add(newSetting);
                    SortGameSettings();
                }
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (GameDataGrid.SelectedItem is GameSetting selected)
            {
                var dialog = new AddEditGameSettingWindow();
                dialog.PriorityTextBox.Text = selected.Priority.ToString();
                dialog.GameNameTextBox.Text = selected.Name;
                dialog.GameIdTextBox.Text = selected.Id;
                dialog.ExecutableTextBox.Text = selected.ExecutableName;
                if (!string.IsNullOrEmpty(selected.BoxArtPath) && File.Exists(selected.BoxArtPath))
                {
                    dialog.BoxArtImage.Source = new BitmapImage(new Uri(selected.BoxArtPath));
                }
                if (dialog.ShowDialog() == true)
                {
                    var edited = dialog.GameSetting;
                    var idx = _gameSettings.IndexOf(selected);
                    if (idx >= 0 && edited != null)
                    {
                        // 檢查 ExecutableName 是否重複（排除自己）
                        if (_gameSettings.Where((g, i) => i != idx).Any(g => string.Equals(g.ExecutableName, edited.ExecutableName, StringComparison.OrdinalIgnoreCase)))
                        {
                            MessageBox.Show($"執行檔名稱 '{edited.ExecutableName}' 已存在，請勿重複。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        _gameSettings[idx] = edited;
                        SortGameSettings();
                    }
                }
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (GameDataGrid.SelectedItem is GameSetting selected)
            {
                if (MessageBox.Show($"確定要刪除 {selected.Name} 嗎？", "確認刪除", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _gameSettings.Remove(selected);
                }
            }
        }

        private void SortGameSettings()
        {
            var sorted = new ObservableCollection<GameSetting>(
                _gameSettings.OrderByDescending(g => g.Priority).ThenBy(g => g.Name)
            );
            _gameSettings.Clear();
            foreach (var item in sorted)
                _gameSettings.Add(item);
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveGameSettings();
            base.OnClosed(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_scanTimer != null && _scanTimer.Enabled)
            {
                _scanTimer.Stop();
                _scanTimer.Dispose();
                _scanTimer = null;
            }
            base.OnClosing(e);
        }
    }
}