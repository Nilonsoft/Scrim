using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace Scrim.Audio {
    public enum EventTriggerType {
        Hourly,
        Daily,
        Interval
    }

    public enum ScheduledActionType {
        PlayJingle,
        PostChatMessage,
        SwitchTheme,
        TriggerPoll
    }

    public class ScheduledEvent {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "Hourly Station ID";
        public EventTriggerType TriggerType { get; set; } = EventTriggerType.Hourly;
        public int Minute { get; set; } = 0; // 0-59 (for hourly or daily)
        public int Hour { get; set; } = 12; // 0-23 (for daily)
        public int IntervalMinutes { get; set; } = 30; // for interval
        public ScheduledActionType ActionType { get; set; } = ScheduledActionType.PlayJingle;
        public string ActionPayload { get; set; } = "Airhorn";
        public bool IsEnabled { get; set; } = true;
        public DateTime? LastExecuted { get; set; }

        public DateTime CalculateNextExecution(DateTime fromTime) {
            switch (TriggerType) {
                case EventTriggerType.Hourly: {
                    var next = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, fromTime.Hour, Minute, 0);
                    if (next <= fromTime) {
                        next = next.AddHours(1);
                    }
                    return next;
                }
                case EventTriggerType.Daily: {
                    var next = new DateTime(fromTime.Year, fromTime.Month, fromTime.Day, Hour, Minute, 0);
                    if (next <= fromTime) {
                        next = next.AddDays(1);
                    }
                    return next;
                }
                case EventTriggerType.Interval:
                default: {
                    int interval = Math.Max(1, IntervalMinutes);
                    return LastExecuted.HasValue ? LastExecuted.Value.AddMinutes(interval) : fromTime.AddMinutes(interval);
                }
            }
        }
    }

    /// <summary>
    /// Automated Show Clock & Event Scheduler Service.
    /// Inspired by SAM Broadcaster PRO's Event Scheduler.
    /// </summary>
    public class EventSchedulerService : IDisposable {
        public List<ScheduledEvent> Events { get; set; } = new();

        private readonly System.Timers.Timer _checkTimer;
        public event EventHandler<ScheduledEvent>? EventTriggered;
        public event EventHandler? EventsChanged;

        public EventSchedulerService() {
            // Add default sample events if empty
            Events.Add(new ScheduledEvent {
                Name = "Top-of-Hour Station Sweep",
                TriggerType = EventTriggerType.Hourly,
                Minute = 0,
                ActionType = ScheduledActionType.PlayJingle,
                ActionPayload = "Airhorn",
                IsEnabled = false
            });

            Events.Add(new ScheduledEvent {
                Name = "Bottom-of-Hour Chat Announcement",
                TriggerType = EventTriggerType.Hourly,
                Minute = 30,
                ActionType = ScheduledActionType.PostChatMessage,
                ActionPayload = "📻 You are listening to Scrim Live Radio! Send your song requests in the chat.",
                IsEnabled = false
            });

            _checkTimer = new System.Timers.Timer(1000);
            _checkTimer.Elapsed += OnTimerElapsed;
            _checkTimer.Start();
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e) {
            var now = DateTime.Now;
            foreach (var evt in Events.Where(ev => ev.IsEnabled).ToList()) {
                var next = evt.CalculateNextExecution(evt.LastExecuted ?? now.AddSeconds(-2));
                if (now >= next && (!evt.LastExecuted.HasValue || (now - evt.LastExecuted.Value).TotalSeconds >= 45)) {
                    evt.LastExecuted = now;
                    EventTriggered?.Invoke(this, evt);
                    EventsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public (ScheduledEvent? Event, TimeSpan TimeRemaining) GetNextUpcomingEvent() {
            var now = DateTime.Now;
            ScheduledEvent? closest = null;
            TimeSpan shortest = TimeSpan.MaxValue;

            foreach (var ev in Events.Where(e => e.IsEnabled)) {
                var next = ev.CalculateNextExecution(now);
                var diff = next - now;
                if (diff < shortest && diff >= TimeSpan.Zero) {
                    shortest = diff;
                    closest = ev;
                }
            }

            return (closest, shortest == TimeSpan.MaxValue ? TimeSpan.Zero : shortest);
        }

        public void AddEvent(ScheduledEvent ev) {
            Events.Add(ev);
            EventsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveEvent(string id) {
            Events.RemoveAll(e => e.Id == id);
            EventsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() {
            _checkTimer.Stop();
            _checkTimer.Dispose();
        }
    }
}
