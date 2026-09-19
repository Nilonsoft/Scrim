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
            services.AddSingleton<MultiFormatTranscoder>();
            services.AddSingleton<AudioDuckingMixer>();
            services.AddTransient<ProcessLoopbackCapture>();
            services.AddTransient<MicrophoneCaptureService>();

            // Server & Metadata
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

            base.OnExit(e);
        }
    }
}
