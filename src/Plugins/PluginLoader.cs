using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Scrim.Plugins {
    public class PluginItem {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string AssemblyPath { get; set; } = string.Empty;
        public string PluginType { get; set; } = "CSharp";
        public bool IsEnabled { get; set; } = true;
        public IScrimPlugin? Instance { get; set; }
    }

    public class PluginLoader : IScrimHost {
        private readonly List<PluginItem> _pluginItems = new();
        private readonly HashSet<string> _disabledPluginIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _pluginsDir;
        private readonly string _stateFilePath;
        private readonly object _lock = new();

        public IReadOnlyList<PluginItem> Plugins {
            get {
                lock (_lock) {
                    return _pluginItems.ToList();
                }
            }
        }

        public IReadOnlyList<IScrimPlugin> LoadedPlugins {
            get {
                lock (_lock) {
                    return _pluginItems
                        .Where(p => p.IsEnabled && p.Instance != null)
                        .Select(p => p.Instance!)
                        .ToList();
                }
            }
        }

        public event EventHandler? PluginsChanged;

        // IScrimHost Implementation
        public Scrim.Server.BroadcastHub Broadcast { get; }
        public Scrim.Metadata.IMetadataService Metadata { get; }
        public Scrim.Configuration.IProfileManager ProfileManager { get; }
        public Scrim.Metadata.SongRequestController SongRequests { get; }
        public Scrim.Audio.AudioDuckingMixer AudioMixer { get; }

        public PluginLoader(
            Scrim.Server.BroadcastHub broadcast,
            Scrim.Metadata.IMetadataService metadata,
            Scrim.Configuration.IProfileManager profileManager,
            Scrim.Metadata.SongRequestController songRequests,
            Scrim.Audio.AudioDuckingMixer audioMixer) {
            
            Broadcast = broadcast;
            Metadata = metadata;
            ProfileManager = profileManager;
            SongRequests = songRequests;
            AudioMixer = audioMixer;

            _pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            try {
                if (!Directory.Exists(_pluginsDir)) {
                    Directory.CreateDirectory(_pluginsDir);
                }
            } catch { }

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _stateFilePath = Path.Combine(userProfile, ".scrim", "plugins_state.json");
            LoadState();
        }

        private void LoadState() {
            try {
                if (File.Exists(_stateFilePath)) {
                    string json = File.ReadAllText(_stateFilePath);
                    var list = JsonSerializer.Deserialize<List<string>>(json);
                    if (list != null) {
                        _disabledPluginIds.Clear();
                        foreach (var id in list) {
                            _disabledPluginIds.Add(id);
                        }
                    }
                }
            } catch { }
        }

        private void SaveState() {
            try {
                string dir = Path.GetDirectoryName(_stateFilePath) ?? "";
                if (!Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(_disabledPluginIds.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            } catch { }
        }

        public void LoadPlugins() {
            lock (_lock) {
                var searchDirs = new List<string>();
                if (Directory.Exists(_pluginsDir)) {
                    searchDirs.Add(_pluginsDir);
                }

                try {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var userPluginsDir = Path.Combine(userProfile, ".scrim", "plugins");
                    if (!Directory.Exists(userPluginsDir)) {
                        Directory.CreateDirectory(userPluginsDir);
                    }
                    if (Directory.Exists(userPluginsDir)) {
                        searchDirs.Add(userPluginsDir);
                    }
                } catch { }

                foreach (var dir in searchDirs) {
                    // 1. Discover and load C# DLL plugins
                    try {
                        var dlls = Directory.GetFiles(dir, "*.dll");
                        foreach (var dll in dlls) {
                            try {
                                var assembly = Assembly.LoadFrom(dll);
                                var pluginTypes = assembly.GetTypes()
                                    .Where(t => typeof(IScrimPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                                foreach (var type in pluginTypes) {
                                    if (Activator.CreateInstance(type) is IScrimPlugin plugin) {
                                        string id = plugin.Name;
                                        bool isEnabled = !_disabledPluginIds.Contains(id);

                                        var item = new PluginItem {
                                            Id = id,
                                            Name = plugin.Name,
                                            Version = plugin.Version,
                                            Author = plugin.Author,
                                            AssemblyPath = dll,
                                            PluginType = "CSharp",
                                            IsEnabled = isEnabled,
                                            Instance = plugin
                                        };

                                        _pluginItems.Add(item);

                                        if (isEnabled) {
                                            try {
                                                plugin.Initialize(this);
                                            } catch (Exception ex) {
                                                Console.WriteLine($"[PluginLoader] Error initializing {plugin.Name}: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                            } catch {
                                // Ignore load errors for individual DLLs
                            }
                        }
                    } catch { }

                    // 2. Discover and load Python plugin folders
                    try {
                        var subDirs = Directory.GetDirectories(dir);
                        foreach (var subDir in subDirs) {
                            try {
                                var pyPlugin = PythonPlugin.FromDirectory(subDir);
                                if (pyPlugin != null) {
                                    string id = pyPlugin.Id;
                                    bool isEnabled = !_disabledPluginIds.Contains(id);

                                    var item = new PluginItem {
                                        Id = id,
                                        Name = pyPlugin.Name,
                                        Version = pyPlugin.Version,
                                        Author = pyPlugin.Author,
                                        AssemblyPath = pyPlugin.ScriptPath,
                                        PluginType = "Python",
                                        IsEnabled = isEnabled,
                                        Instance = pyPlugin
                                    };

                                    _pluginItems.Add(item);

                                    if (isEnabled) {
                                        try {
                                            pyPlugin.Initialize(this);
                                        } catch (Exception ex) {
                                            Console.WriteLine($"[PluginLoader] Error initializing Python plugin {pyPlugin.Name}: {ex.Message}");
                                        }
                                    }
                                }
                            } catch { }
                        }
                    } catch { }

                    // 3. Discover and load standalone .py script plugins
                    try {
                        var pyFiles = Directory.GetFiles(dir, "*.py");
                        foreach (var pyFile in pyFiles) {
                            string fileName = Path.GetFileName(pyFile);
                            if (string.Equals(fileName, "scrim.py", StringComparison.OrdinalIgnoreCase)) {
                                continue;
                            }

                            try {
                                var pyPlugin = PythonPlugin.FromScript(pyFile);
                                string id = pyPlugin.Id;
                                bool isEnabled = !_disabledPluginIds.Contains(id);

                                var item = new PluginItem {
                                    Id = id,
                                    Name = pyPlugin.Name,
                                    Version = pyPlugin.Version,
                                    Author = pyPlugin.Author,
                                    AssemblyPath = pyPlugin.ScriptPath,
                                    PluginType = "Python",
                                    IsEnabled = isEnabled,
                                    Instance = pyPlugin
                                };

                                _pluginItems.Add(item);

                                if (isEnabled) {
                                    try {
                                        pyPlugin.Initialize(this);
                                    } catch (Exception ex) {
                                        Console.WriteLine($"[PluginLoader] Error initializing Python script {pyPlugin.Name}: {ex.Message}");
                                    }
                                }
                            } catch { }
                        }
                    } catch { }
                }
            }
        }

        public void TogglePlugin(string pluginId) {
            lock (_lock) {
                var item = _pluginItems.FirstOrDefault(p => 
                    string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(p.Name, pluginId, StringComparison.OrdinalIgnoreCase));

                if (item == null) {
                    return;
                }

                if (item.IsEnabled) {
                    try {
                        item.Instance?.Shutdown();
                    } catch (Exception ex) {
                        Console.WriteLine($"[PluginLoader] Error shutting down {item.Name}: {ex.Message}");
                    }
                    item.IsEnabled = false;
                    _disabledPluginIds.Add(item.Id);
                } else {
                    try {
                        item.Instance?.Initialize(this);
                    } catch (Exception ex) {
                        Console.WriteLine($"[PluginLoader] Error initializing {item.Name}: {ex.Message}");
                    }
                    item.IsEnabled = true;
                    _disabledPluginIds.Remove(item.Id);
                }

                SaveState();
            }

            PluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReloadPlugins() {
            UnloadPlugins();
            LoadPlugins();
            PluginsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UnloadPlugins() {
            lock (_lock) {
                foreach (var item in _pluginItems) {
                    if (item.IsEnabled) {
                        try {
                            item.Instance?.Shutdown();
                        } catch { }
                    }
                    if (item.Instance is IDisposable disposable) {
                        try {
                            disposable.Dispose();
                        } catch { }
                    }
                }
                _pluginItems.Clear();
            }
        }
    }
}
