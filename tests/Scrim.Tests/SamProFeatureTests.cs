using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Scrim.Audio;
using Scrim.Configuration;
using Scrim.Server;
using Xunit;

namespace Scrim.Tests {
    public class SamProFeatureTests {
        [Fact]
        public void DualDeck_CrossfaderLinearGains_CalculatesExpectedPercentages() {
            var mixer = new DualDeckMixerService {
                Curve = CrossfadeCurve.Linear
            };

            // Full left (Deck A)
            mixer.CrossfaderPosition = -1.0f;
            var (gainA1, gainB1) = mixer.GetCrossfaderGains();
            Assert.Equal(1.0f, gainA1, 2);
            Assert.Equal(0.0f, gainB1, 2);

            // Center (50/50)
            mixer.CrossfaderPosition = 0.0f;
            var (gainA2, gainB2) = mixer.GetCrossfaderGains();
            Assert.Equal(0.5f, gainA2, 2);
            Assert.Equal(0.5f, gainB2, 2);

            // Full right (Deck B)
            mixer.CrossfaderPosition = 1.0f;
            var (gainA3, gainB3) = mixer.GetCrossfaderGains();
            Assert.Equal(0.0f, gainA3, 2);
            Assert.Equal(1.0f, gainB3, 2);
        }

        [Fact]
        public void DualDeck_ConstantPowerGains_MaintainsEqualLoudness() {
            var mixer = new DualDeckMixerService {
                Curve = CrossfadeCurve.ConstantPower,
                CrossfaderPosition = 0.0f // Center
            };

            var (gainA, gainB) = mixer.GetCrossfaderGains();
            // Constant power at center is approx 0.707 (cos(pi/4))
            Assert.InRange(gainA, 0.70f, 0.72f);
            Assert.InRange(gainB, 0.70f, 0.72f);
            // Sum of squares should equal 1.0 (constant power)
            float power = (gainA * gainA) + (gainB * gainB);
            Assert.InRange(power, 0.99f, 1.01f);
        }

        [Fact]
        public void DualDeck_CutCurve_PreservesMasterUntilNearExtreme() {
            var mixer = new DualDeckMixerService {
                Curve = CrossfadeCurve.Cut,
                CrossfaderPosition = 0.0f // Center
            };

            var (gainA, gainB) = mixer.GetCrossfaderGains();
            Assert.Equal(1.0f, gainA);
            Assert.Equal(1.0f, gainB);

            mixer.CrossfaderPosition = 0.8f; // Near full B
            var (gainA2, gainB2) = mixer.GetCrossfaderGains();
            Assert.Equal(0.0f, gainA2);
            Assert.Equal(1.0f, gainB2);
        }

        [Fact]
        public void DualDeck_CueAndPlay_UpdatesStateCorrectly() {
            var mixer = new DualDeckMixerService();

            Assert.False(mixer.DeckA.IsPlaying);
            mixer.TogglePlay("A");
            Assert.True(mixer.DeckA.IsPlaying);

            mixer.SetCue("A");
            Assert.True(mixer.DeckA.IsCueSet);

            mixer.Stop("A");
            Assert.False(mixer.DeckA.IsPlaying);
            Assert.Equal(TimeSpan.Zero, mixer.DeckA.Position);
        }

        [Fact]
        public void EventScheduler_CalculatesNextExecution_Accurately() {
            var baseTime = new DateTime(2026, 9, 21, 14, 15, 0); // 14:15:00

            // Hourly at minute 0
            var hourlyEv = new ScheduledEvent {
                TriggerType = EventTriggerType.Hourly,
                Minute = 0
            };
            var nextHourly = hourlyEv.CalculateNextExecution(baseTime);
            Assert.Equal(new DateTime(2026, 9, 21, 15, 0, 0), nextHourly);

            // Daily at 08:30
            var dailyEv = new ScheduledEvent {
                TriggerType = EventTriggerType.Daily,
                Hour = 8,
                Minute = 30
            };
            var nextDaily = dailyEv.CalculateNextExecution(baseTime);
            Assert.Equal(new DateTime(2026, 9, 22, 8, 30, 0), nextDaily);

            // Interval every 20 minutes
            var intervalEv = new ScheduledEvent {
                TriggerType = EventTriggerType.Interval,
                IntervalMinutes = 20
            };
            var nextInterval = intervalEv.CalculateNextExecution(baseTime);
            Assert.Equal(new DateTime(2026, 9, 21, 14, 35, 0), nextInterval);
        }

        [Fact]
        public void DeadAirRecovery_ArmAndReset_WorksAsExpected() {
            using var deadAir = new DeadAirRecoveryService {
                IsEnabled = true,
                SilenceThresholdSeconds = 5,
                SilenceLevelThreshold = 0.005f
            };

            Assert.True(deadAir.IsEnabled);
            Assert.False(deadAir.IsInDeadAirState);

            // Audio level above threshold
            deadAir.UpdateCurrentLevel(0.5f);
            Assert.Equal(0, deadAir.CurrentSilenceDurationSeconds);

            deadAir.ResetAlarm();
            Assert.False(deadAir.IsInDeadAirState);
            Assert.Equal(0, deadAir.CurrentSilenceDurationSeconds);
        }

        [Fact]
        public void BroadcastHub_ClientRegistrationAndDisconnect_UpdatesActiveListeners() {
            var hub = new BroadcastHub();

            using var cts = new CancellationTokenSource();
            var worker = hub.RegisterClient("192.168.1.100", "VLC/3.0.18", "/live", cts);

            var active = hub.GetActiveClients();
            Assert.Single(active);
            var first = System.Linq.Enumerable.First(active);
            Assert.Equal("192.168.1.100", first.IpAddress);
            Assert.Equal("VLC/3.0.18", first.UserAgent);
            Assert.Equal("/live", first.MountPoint);

            // Disconnect client
            hub.DisconnectClient(worker.ClientId);
            Assert.True(cts.IsCancellationRequested);

            // Clean up
            hub.UnregisterClient(worker.ClientId);
            Assert.Empty(hub.GetActiveClients());
        }
    }
}
