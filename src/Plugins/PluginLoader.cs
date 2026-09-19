using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Scrim.Plugins {
    public class PluginLoader : IScrimHost {
        private readonly List<IScrimPlugin> _plugins = new();
        private readonly string _pluginsDir;

        public IReadOnlyList<IScrimPlugin> LoadedPlugins => _plugins;

        // IScrimHost Implementation
        public Scrim.Server.BroadcastHub Broadcast { get; }
        public Scrim.Metadata.IMetadataService Metadata { get; }
        public Scrim.Configuration.IProfileManager ProfileManager { get; }
        public Scrim.Metadata.SongRequestController SongRequests { get; }

        public PluginLoader(
            Scrim.Server.BroadcastHub broadcast,
            Scrim.Metadata.IMetadataService metadata,
            Scrim.Configuration.IProfileManager profileManager,
            Scrim.Metadata.SongRequestController songRequests) {
            
            Broadcast = broadcast;
            Metadata = metadata;
            ProfileManager = profileManager;
            SongRequests = songRequests;

            _pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            if (!Directory.Exists(_pluginsDir)) {
                Directory.CreateDirectory(_pluginsDir);
            }
        }

        public void LoadPlugins() {
            var dlls = Directory.GetFiles(_pluginsDir, "*.dll");
            foreach (var dll in dlls) {
                try {
                    var assembly = Assembly.LoadFrom(dll);
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IScrimPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in pluginTypes) {
                        if (Activator.CreateInstance(type) is IScrimPlugin plugin) {
                            _plugins.Add(plugin);
                            plugin.Initialize(this);
                        }
                    }
                } catch {
                    // Ignore load errors for now
                }
            }
        }

        public void UnloadPlugins() {
            foreach (var plugin in _plugins) {
                plugin.Shutdown();
            }
            _plugins.Clear();
        }
    }
}
