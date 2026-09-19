using Scrim.Configuration;
using Scrim.Metadata;
using Scrim.Server;

namespace Scrim.Plugins {
    public interface IScrimHost {
        BroadcastHub Broadcast { get; }
        IMetadataService Metadata { get; }
        IProfileManager ProfileManager { get; }
        SongRequestController SongRequests { get; }
    }
}
