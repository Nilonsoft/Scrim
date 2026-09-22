using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Scrim.Audio;
using Scrim.Configuration;
using Scrim.Encoding;
using Scrim.Metadata;
using Scrim.Plugins;
using Scrim.Server;
using Scrim.UI.Windows;
using Scrim.Updates;

namespace Scrim {
    public partial class App : Application {
        public IServiceProvider Services { get; }

        public App() {
            // Configure WebView2 User Data Folder in LocalAppData to avoid permission issues
            try {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var webView2Dir = Path.Combine(localAppData, "Scrim", "WebView2");
                if (!Directory.Exists(webView2Dir)) {
                    Directory.CreateDirectory(webView2Dir);
                }
                Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webView2Dir);
            } catch { }

            // Ensure all required user profile directories exist
            try {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var scrimDir = Path.Combine(userProfile, ".scrim");
                string[] dirs = new[] {
                    scrimDir,
                    Path.Combine(scrimDir, "assets"),
                    Path.Combine(scrimDir, "themes"),
                    Path.Combine(scrimDir, "voice_effects"),
                    Path.Combine(scrimDir, "plugins"),
                    Path.Combine(scrimDir, "playlists")
                };
                foreach (var dir in dirs) {
                    if (!Directory.Exists(dir)) {
                        Directory.CreateDirectory(dir);
                    }
                }
            } catch { }

            // Global exception handling to prevent silent process crashes
            DispatcherUnhandledException += (s, args) => {
                LogCrash(args.Exception);
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nDetails have been logged to ~/.scrim/crash.log.",
                    "Scrim Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) => {
                if (args.ExceptionObject is Exception ex) {
                    LogCrash(ex);
                }
            };

            var services = new ServiceCollection();
            
            // Blazor specific
            services.AddWpfBlazorWebView();

            // Configuration
            services.AddSingleton<IProfileManager, ProfileManager>();
            services.AddSingleton<IThemeService, ThemeService>();

            // Audio & Encoding
            services.AddSingleton<IFFmpegService, FFmpegService>();
            services.AddSingleton<VoiceEffectLoader>();
            services.AddSingleton<MultiFormatTranscoder>();
            services.AddSingleton<AudioDuckingMixer>();
            services.AddTransient<ProcessLoopbackCapture>();
            services.AddTransient<MicrophoneCaptureService>();
            services.AddSingleton<LocalAudioRoutingService>();
            services.AddSingleton<VirtualAudioDeviceService>();
            services.AddSingleton<DualDeckMixerService>();
            services.AddSingleton<EventSchedulerService>();
            services.AddSingleton<DeadAirRecoveryService>();
            services.AddSingleton<LocalMusicPlayerService>();

            // Server & Metadata
            services.AddSingleton<INetworkDiscoveryService, NetworkDiscoveryService>();
            services.AddSingleton<BroadcastHub>();
            services.AddSingleton<HttpStreamServer>();
            services.AddSingleton<IMetadataService, WindowsMediaMetadataService>();
            services.AddSingleton<SongRequestController>();
            services.AddSingleton<ILiveChatService, LiveChatService>();
            services.AddSingleton<ISongReactionService, SongReactionService>();
            services.AddSingleton<ISongHistoryService, SongHistoryService>();

            // Updates
            services.AddSingleton<IUpdateService, UpdateService>();

            // Plugins
            services.AddSingleton<PluginLoader>();

            Services = services.BuildServiceProvider();
            
            Resources.Add("services", Services);
        }

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            // Handle silent background update check if triggered by Windows Task Scheduler
            bool isSilentCheck = false;
            for (int i = 0; i < e.Args.Length; i++) {
                if (string.Equals(e.Args[i], "--check-updates-silent", StringComparison.OrdinalIgnoreCase)) {
                    isSilentCheck = true;
                    break;
                }
            }

            if (isSilentCheck) {
                _ = HandleSilentUpdateCheckAsync();
                return;
            }

            var profileManager = Services.GetRequiredService<IProfileManager>();
            if (e.Args.Length > 0) {
                for (int i = 0; i < e.Args.Length; i++) {
                    if ((e.Args[i] == "--profile" || e.Args[i] == "-p") && i + 1 < e.Args.Length) {
                        string profileName = e.Args[i + 1];
                        profileManager.LoadProfile(profileName);
                    } else if (e.Args[i] == "--port" && i + 1 < e.Args.Length && int.TryParse(e.Args[i + 1], out int p)) {
                        profileManager.CurrentProfile.Port = p;
                    }
                }
            }

            var effectLoader = Services.GetRequiredService<VoiceEffectLoader>();
            effectLoader.LoadEffects();

            var pluginLoader = Services.GetRequiredService<PluginLoader>();
            pluginLoader.LoadPlugins();

            var mainWindow = new MainWindow();
            mainWindow.Show();

            // Synchronize daily background scheduled task
            var updateService = Services.GetRequiredService<IUpdateService>();
            Task.Run(() => updateService.EnsureDailyScheduledTask());

            // Run asynchronous update check on startup without delaying UI launch
            _ = CheckForUpdatesOnStartupAsync(updateService);
        }

        private async Task HandleSilentUpdateCheckAsync() {
            try {
                var updateService = Services.GetRequiredService<IUpdateService>();
                var result = await updateService.CheckForUpdatesAsync();
                if (result.Status == UpdateStatus.UpdateAvailable && result.UpdateInfo != null) {
                    var dialog = new UpdateDialog(result.UpdateInfo, updateService);
                    dialog.ShowDialog();
                }
            } catch { }

            Shutdown();
        }

        private async Task CheckForUpdatesOnStartupAsync(IUpdateService updateService) {
            try {
                // Brief 2-second delay allowing the main window and Blazor view to render smoothly
                await Task.Delay(2000);

                var result = await updateService.CheckForUpdatesAsync();
                if (result.Status == UpdateStatus.UpdateAvailable && result.UpdateInfo != null) {
                    await Dispatcher.InvokeAsync(() => {
                        var dialog = new UpdateDialog(result.UpdateInfo, updateService);
                        dialog.Owner = MainWindow;
                        dialog.ShowDialog();
                    });
                }
            } catch { }
        }

        protected override void OnExit(ExitEventArgs e) {
            var pluginLoader = Services.GetService<PluginLoader>();
            pluginLoader?.UnloadPlugins();

            var streamServer = Services.GetService<HttpStreamServer>();
            streamServer?.Stop();

            var profileManager = Services.GetService<IProfileManager>();
            if (profileManager?.CurrentProfile != null) {
                profileManager.SaveProfile(profileManager.CurrentProfile);
            }

            var localAudioRouting = Services.GetService<LocalAudioRoutingService>();
            localAudioRouting?.RestoreAllMuted();

            base.OnExit(e);
        }

        private static void LogCrash(Exception ex) {
            try {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var logDir = Path.Combine(userProfile, ".scrim");
                if (!Directory.Exists(logDir)) {
                    Directory.CreateDirectory(logDir);
                }
                var logFile = Path.Combine(logDir, "crash.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRASH: {ex}\n\n");
            } catch { }
        }
    }
}
