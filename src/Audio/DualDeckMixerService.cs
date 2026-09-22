using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scrim.Audio {
    public enum CrossfadeCurve {
        ConstantPower,
        Linear,
        Cut
    }

    public class DeckState {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "No Track Loaded";
        public string Artist { get; set; } = "";
        public string FilePath { get; set; } = "";
        public bool IsPlaying { get; set; } = false;
        public TimeSpan Position { get; set; } = TimeSpan.Zero;
        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(3.5);
        public float Volume { get; set; } = 1.0f;
        public float PitchPercent { get; set; } = 0.0f; // -8% to +8%
        public bool IsCueSet { get; set; } = false;
        public TimeSpan CuePosition { get; set; } = TimeSpan.Zero;
    }

    /// <summary>
    /// Dual Decks (Deck A & Deck B) mixing and crossfader automation service.
    /// Inspired by SAM Broadcaster PRO's dual deck console.
    /// </summary>
    public class DualDeckMixerService {
        public DeckState DeckA { get; } = new() { Id = "A", Title = "Deck A (Master)" };
        public DeckState DeckB { get; } = new() { Id = "B", Title = "Deck B (Standby)" };

        // Crossfader: -1.0 (Full Deck A) ... 0.0 (Center) ... +1.0 (Full Deck B)
        public float CrossfaderPosition { get; set; } = -1.0f;
        public CrossfadeCurve Curve { get; set; } = CrossfadeCurve.ConstantPower;
        public bool IsAutoCrossfading { get; private set; } = false;

        private CancellationTokenSource? _autoCrossfadeCts;

        public event EventHandler? StateChanged;

        public (float GainA, float GainB) GetCrossfaderGains() {
            float pos = Math.Clamp(CrossfaderPosition, -1.0f, 1.0f);

            switch (Curve) {
                case CrossfadeCurve.Linear: {
                    // -1 -> A=1, B=0; 0 -> A=0.5, B=0.5; +1 -> A=0, B=1
                    float norm = (pos + 1.0f) * 0.5f; // 0.0 to 1.0
                    return (1.0f - norm, norm);
                }
                case CrossfadeCurve.Cut: {
                    // Fast cut: -1 to -0.1 full A; -0.1 to +0.1 both; +0.1 to +1 full B
                    float gainA = pos > 0.05f ? 0.0f : 1.0f;
                    float gainB = pos < -0.05f ? 0.0f : 1.0f;
                    return (gainA, gainB);
                }
                case CrossfadeCurve.ConstantPower:
                default: {
                    // Equal loudness curve: cos/sin quarter circle
                    float angle = (pos + 1.0f) * 0.25f * MathF.PI; // 0 to PI/2
                    float gainA = MathF.Cos(angle);
                    float gainB = MathF.Sin(angle);
                    return (gainA, gainB);
                }
            }
        }

        public void SetCue(string deckId) {
            var deck = deckId == "A" ? DeckA : DeckB;
            deck.CuePosition = deck.Position;
            deck.IsCueSet = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ReturnToCue(string deckId) {
            var deck = deckId == "A" ? DeckA : DeckB;
            if (deck.IsCueSet) {
                deck.Position = deck.CuePosition;
                deck.IsPlaying = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void TogglePlay(string deckId) {
            var deck = deckId == "A" ? DeckA : DeckB;
            deck.IsPlaying = !deck.IsPlaying;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Stop(string deckId) {
            var deck = deckId == "A" ? DeckA : DeckB;
            deck.IsPlaying = false;
            deck.Position = TimeSpan.Zero;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task TriggerAutoCrossfadeAsync(int durationSeconds = 4) {
            _autoCrossfadeCts?.Cancel();
            _autoCrossfadeCts = new CancellationTokenSource();
            var token = _autoCrossfadeCts.Token;

            IsAutoCrossfading = true;
            StateChanged?.Invoke(this, EventArgs.Empty);

            // Determine target: if on left side (< 0), fade to right (+1.0); otherwise fade to left (-1.0)
            float startPos = CrossfaderPosition;
            float targetPos = startPos < 0.0f ? 1.0f : -1.0f;

            // Ensure incoming deck is playing
            if (targetPos > 0.0f && !DeckB.IsPlaying) {
                DeckB.IsPlaying = true;
            } else if (targetPos < 0.0f && !DeckA.IsPlaying) {
                DeckA.IsPlaying = true;
            }

            int steps = Math.Max(20, durationSeconds * 25);
            int stepDelayMs = (durationSeconds * 1000) / steps;

            try {
                for (int i = 1; i <= steps; i++) {
                    if (token.IsCancellationRequested) break;
                    float progress = (float)i / steps;
                    // Smooth S-curve transition
                    float smoothT = progress * progress * (3.0f - 2.0f * progress);
                    CrossfaderPosition = startPos + (targetPos - startPos) * smoothT;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                    await Task.Delay(stepDelayMs, token);
                }

                CrossfaderPosition = targetPos;

                // Stop the outgoing deck once crossfade is complete
                if (targetPos > 0.0f) {
                    DeckA.IsPlaying = false;
                } else {
                    DeckB.IsPlaying = false;
                }
            } catch (OperationCanceledException) { } finally {
                IsAutoCrossfading = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
