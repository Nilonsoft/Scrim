using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Scrim.Audio;
using Scrim.Configuration;
using Scrim.Encoding;
using Scrim.Metadata;
using Scrim.Plugins;
using Scrim.Server;

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

            // Server & Metadata
            services.AddSingleton<INetworkDiscoveryService, NetworkDiscoveryService>();
            services.AddSingleton<BroadcastHub>();
            services.AddSingleton<HttpStreamServer>();
            services.AddSingleton<IMetadataService, WindowsMediaMetadataService>();
            services.AddSingleton<SongRequestController>();
            services.AddSingleton<ILiveChatService, LiveChatService>();
            services.AddSingleton<ISongReactionService, SongReactionService>();
            services.AddSingleton<ISongHistoryService, SongHistoryService>();

            // Plugins
            services.AddSingleton<PluginLoader>();

            Services = services.BuildServiceProvider();
            
            Resources.Add("services", Services);
        }

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

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
        }

        protected override void OnExit(ExitEventArgs e) {
            var pluginLoader = Services.GetRequiredService<PluginLoader>();
            pluginLoader.UnloadPlugins();

            var streamServer = Services.GetRequiredService<HttpStreamServer>();
            streamServer.Stop();

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
