using System;
using System.Timers;

namespace Scrim.Audio {
    /// <summary>
    /// Dead-Air Detection & Automated Fallback Recovery Service.
    /// Monitors output audio energy and automatically triggers emergency backup audio
    /// or DJ alarms when dead air occurs, inspired by SAM Broadcaster PRO.
    /// </summary>
    public class DeadAirRecoveryService : IDisposable {
        public bool IsEnabled { get; set; } = false;
        public int SilenceThresholdSeconds { get; set; } = 10; // 5s, 10s, 15s
        public float SilenceLevelThreshold { get; set; } = 0.002f; // -54 dB
        public bool AutoPlayBackup { get; set; } = true;
        public string FallbackJingleName { get; set; } = "Airhorn";
        public string BackupAudioFilePath { get; set; } = "";
        public bool PlayAudibleAlarm { get; set; } = true;

        public int CurrentSilenceDurationSeconds { get; private set; } = 0;
        public bool IsInDeadAirState { get; private set; } = false;
        public int RecoveryTriggerCount { get; private set; } = 0;

        public event EventHandler? DeadAirTriggered;
        public event EventHandler? DeadAirRecovered;
        public event EventHandler? StateChanged;

        private readonly System.Timers.Timer _timer;
        private float _lastSampledLevel = 0.0f;

        public DeadAirRecoveryService() {
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += OnTimerTick;
            _timer.Start();
        }

        public void UpdateCurrentLevel(float maxPeak) {
            _lastSampledLevel = maxPeak;
        }

        private void OnTimerTick(object? sender, ElapsedEventArgs e) {
            if (!IsEnabled) {
                if (CurrentSilenceDurationSeconds > 0 || IsInDeadAirState) {
                    CurrentSilenceDurationSeconds = 0;
                    IsInDeadAirState = false;
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            if (_lastSampledLevel < SilenceLevelThreshold) {
                CurrentSilenceDurationSeconds++;

                if (CurrentSilenceDurationSeconds >= SilenceThresholdSeconds && !IsInDeadAirState) {
                    IsInDeadAirState = true;
                    RecoveryTriggerCount++;
                    DeadAirTriggered?.Invoke(this, EventArgs.Empty);
                }
            } else {
                if (IsInDeadAirState) {
                    IsInDeadAirState = false;
                    DeadAirRecovered?.Invoke(this, EventArgs.Empty);
                }
                CurrentSilenceDurationSeconds = 0;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetAlarm() {
            CurrentSilenceDurationSeconds = 0;
            IsInDeadAirState = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
