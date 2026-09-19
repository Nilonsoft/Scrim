using System;
using System.Collections.Generic;

namespace Scrim.Plugins {
    public interface IScrimPlugin {
        string Name { get; }
        string Version { get; }
        string Author { get; }
        void Initialize(IScrimHost host);
        void Shutdown();
    }

    public interface IUiExtension {
        string CardTitle { get; }
        Type ComponentType { get; }
    }
}
