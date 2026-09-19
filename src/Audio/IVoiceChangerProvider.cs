namespace Scrim.Audio {
    public interface IVoiceChangerProvider {
        string Id { get; }
        string Name { get; }
        void Process(byte[] buffer);
    }
}
