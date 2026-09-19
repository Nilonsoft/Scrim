using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Scrim.Plugins {
    public class PluginLoader {
        private readonly List<IScrimPlugin> _plugins = new();
        private readonly string _pluginsDir;

        public IReadOnlyList<IScrimPlugin> LoadedPlugins => _plugins;

        public PluginLoader() {
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
                            plugin.Initialize();
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
