using System;
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
            var services = new ServiceCollection();
            
            // Blazor specific
            services.AddWpfBlazorWebView();

            // Configuration
            services.AddSingleton<IProfileManager, ProfileManager>();

            // Audio & Encoding
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
    }
}
