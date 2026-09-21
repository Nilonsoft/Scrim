using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Scrim.Updates;

namespace Scrim.UI.Windows {
    public partial class UpdateDialog : Window {
        private readonly UpdateResponse _update;
        private readonly IUpdateService _updateService;
        private CancellationTokenSource? _downloadCts;

        public UpdateDialog(UpdateResponse update, IUpdateService updateService) {
            InitializeComponent();
            _update = update;
            _updateService = updateService;

            TxtCurrentVersion.Text = $"v{update.CurrentVersion}";
            TxtLatestVersion.Text = $"v{update.LatestVersion}";

            if (update.ReleaseDateUtc.HasValue) {
                TxtReleaseDate.Text = update.ReleaseDateUtc.Value.ToLocalTime().ToString("MMM dd, yyyy");
            } else {
                TxtReleaseDate.Text = "";
            }

            if (update.FileSizeBytes > 0) {
                double sizeMb = update.FileSizeBytes / (1024.0 * 1024.0);
                TxtFileSize.Text = $"{sizeMb:F1} MB";
            } else {
                TxtFileSize.Text = "";
            }

            ItemsChangelog.ItemsSource = update.ChangelogSummary;

            if (update.IsMandatory) {
                BtnLater.IsEnabled = false;
                BtnLater.ToolTip = "This update is required to continue using Scrim.";
            }
        }

        private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e) {
            BtnUpdateNow.IsEnabled = false;
            BtnDownloadBrowser.IsEnabled = false;
            BtnLater.IsEnabled = false;
            PanelProgress.Visibility = Visibility.Visible;

            TxtProgressStatus.Text = "Downloading update package...";
            ProgressBarDownload.Value = 0;
            TxtProgressPercent.Text = "0%";

            _downloadCts = new CancellationTokenSource();
            var progress = new Progress<double>(percent => {
                Dispatcher.Invoke(() => {
                    ProgressBarDownload.Value = percent;
                    TxtProgressPercent.Text = $"{(int)percent}%";
                });
            });

            try {
                string msiPath = await _updateService.DownloadUpdateAsync(
                    _update.DownloadUrl,
                    _update.LatestVersion,
                    progress,
                    _downloadCts.Token);

                TxtProgressStatus.Text = "Launching installer and restarting...";
                ProgressBarDownload.IsIndeterminate = true;

                // Execute auto-update routine (closes running instances, runs msiexec, restarts)
                _updateService.ApplyAutoUpdate(msiPath);
            } catch (Exception ex) {
                PanelProgress.Visibility = Visibility.Collapsed;
                BtnUpdateNow.IsEnabled = true;
                BtnDownloadBrowser.IsEnabled = true;
                BtnLater.IsEnabled = !_update.IsMandatory;

                MessageBox.Show(
                    $"Failed to download or execute update:\n\n{ex.Message}\n\nYou can still download the installer directly from the website.",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void BtnDownloadBrowser_Click(object sender, RoutedEventArgs e) {
            try {
                string targetUrl = !string.IsNullOrWhiteSpace(_update.DownloadUrl)
                    ? _update.DownloadUrl
                    : "https://www.nilonsoft.com";

                Process.Start(new ProcessStartInfo {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            } catch (Exception ex) {
                MessageBox.Show($"Unable to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e) {
            _downloadCts?.Cancel();
            Close();
        }

        protected override void OnClosed(EventArgs e) {
            _downloadCts?.Cancel();
            base.OnClosed(e);
        }
    }
}
