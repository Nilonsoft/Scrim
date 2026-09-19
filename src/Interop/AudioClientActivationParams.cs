using System.Runtime.InteropServices;

namespace Scrim.Interop {
    public enum AUDIOCLIENT_ACTIVATION_TYPE {
        AUDIOCLIENT_ACTIVATION_TYPE_DEFAULT = 0,
        AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK = 1
    }

    public enum PROCESS_LOOPBACK_MODE {
        PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE = 0,
        PROCESS_LOOPBACK_MODE_EXCLUDE_TARGET_PROCESS_TREE = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS {
        public uint TargetProcessId;
        public PROCESS_LOOPBACK_MODE ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct AUDIOCLIENT_ACTIVATION_PARAMS {
        [FieldOffset(0)]
        public AUDIOCLIENT_ACTIVATION_TYPE ActivationType;

        [FieldOffset(4)]
        public AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS ProcessLoopbackParams;
    }
}
