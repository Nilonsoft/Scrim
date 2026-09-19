# Scrim Plugin Development Guide

The Scrim Plugin API allows developers to create custom logic and Blazor components that are dynamically loaded into the Scrim dashboard as draggable UI cards. These plugins can tap into the underlying broadcast pipeline to visualize audio, control streams, read current active profile configurations, or interface with external APIs.

## Where Do Plugins Go?

Compiled plugin libraries (`.dll` files) should be placed in the `Plugins` directory located next to your `Scrim.exe` executable.

If you are running the project directly from your IDE during development, this directory is typically found at:
`\src\bin\Debug\net10.0-windows10.0.19041.0\Plugins\`

When the application launches, the `PluginLoader` automatically scans this folder, loads any valid plugin assemblies, and injects them into the Dashboard UI.

## Creating a Plugin

### 1. Scaffold a New Project

Create a new .NET Class Library project targeting the same framework as Scrim (`net10.0-windows10.0.19041.0` or a compatible `.NET 8/9/10` standard depending on your needs).

Reference the `Scrim.dll` assembly or the relevant interfaces in your project.

### 2. The `IScrimHost` Extensibility API

During initialization, your plugin will be provided an `IScrimHost` object. This interface grants access to the core services of Scrim:

```csharp
public interface IScrimHost {
    BroadcastHub Broadcast { get; }
    IMetadataService Metadata { get; }
    IProfileManager ProfileManager { get; }
    SongRequestController SongRequests { get; }
}
```

- **`Broadcast`**: Allows you to check the `ActiveClientCount` or hook into stream events.
- **`Metadata`**: Allows you to retrieve the `CurrentMetadata` (Track, Artist, Album Art) resolving from the active window.
- **`ProfileManager`**: Retrieve the `CurrentProfile` to see what AudioFormat, Bitrate, and PID are currently being broadcasted.
- **`SongRequests`**: Access the live `GetLiveQueue()` to build visualizers, integrations (like Twitch Chat), or automatic song responders.

### 3. Implement the `IScrimPlugin` Interface

The core of any plugin is a class that implements `IScrimPlugin`.

```csharp
using Scrim.Plugins;

namespace MyCustomPlugin {
    public class ExamplePlugin : IScrimPlugin {
        public string Name => "Example Plugin";
        public string Version => "1.0.0";
        public string Author => "DJ John Doe";

        private IScrimHost _host;

        public void Initialize(IScrimHost host) {
            _host = host;
            // E.g. Check active users: int listeners = _host.Broadcast.ActiveClientCount;
        }

        public void Shutdown() {
            // Cleanup resources here
        }
    }
}
```

### 4. Creating a Custom UI Card (Optional)

If you want your plugin to add a custom card to the Scrim drag-and-drop dashboard, you need to:

1. Create a Blazor Razor component (`.razor` file) in your plugin project.
2. Implement the `IUiExtension` interface on your plugin class to tell Scrim about your UI component.

```csharp
using System;
using Scrim.Plugins;

namespace MyCustomPlugin {
    public class ExamplePlugin : IScrimPlugin, IUiExtension {
        public string Name => "Example Visualizer";
        public string Version => "1.0.0";
        public string Author => "DJ John Doe";

        // Implement IUiExtension
        public string CardTitle => "Visualizer Module";
        public Type ComponentType => typeof(MyCustomCardComponent);

        public void Initialize(IScrimHost host) { }
        public void Shutdown() { }
    }
}
```

*Note: Your `MyCustomCardComponent.razor` should ideally follow the aesthetic guidelines in the specification (dark background, #181818 cards).*

### 5. Build and Deploy

Compile your class library. Take the resulting `MyCustomPlugin.dll` (and any related dependencies) and drop them into the Scrim `Plugins/` folder. Restart Scrim, and your plugin card will automatically appear in the right column of the dashboard!
