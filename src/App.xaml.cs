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
