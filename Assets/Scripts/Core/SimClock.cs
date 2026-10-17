using System;

namespace MySmartFarm.Core
{
    /// <summary>Unified simulation clock. 1 real hour = 1 virtual day at 1x. Driven by delta seconds (not frames) or by UTC timestamps (offline).</summary>
    [Serializable]
    public class SimClock
    {
        public const double RealSecondsPerVirtualDay = 3600.0;
        public double VirtualDay;            // fractional; whole part = current day index
        public int Speed = 1;                // 1, 2, 4
        public bool Paused;
        public long LastUpdateUtcTicks;      // persisted
        public bool OfflineProgressEnabled = true;
        public double MaxOfflineHours = 72;  // cap; real hours

        public int WholeDay => (int)Math.Floor(VirtualDay);

        public void SetSpeed(int s) { if (s != 1 && s != 2 && s != 4) throw new ArgumentException("speed must be 1, 2 or 4"); Speed = s; }
        /// <summary>Real seconds to wait for one virtual day at current speed.</summary>
        public double RealSecondsPerDayAtCurrentSpeed => RealSecondsPerVirtualDay / Speed;

        /// <summary>Foreground advance. Returns virtual days added.</summary>
        public double Advance(double realDeltaSeconds, DateTime utcNow)
        {
            LastUpdateUtcTicks = utcNow.Ticks;
            if (Paused || realDeltaSeconds <= 0) return 0;
            double d = realDeltaSeconds * Speed / RealSecondsPerVirtualDay;
            VirtualDay += d; return d;
        }

        /// <summary>Offline catch-up from last saved timestamp. Applied once: updates LastUpdateUtcTicks so a second call yields 0.
        /// A device clock set backwards yields 0 (and re-anchors). Speed multiplier does not apply offline (always 1x).
        /// Limit: a device clock set FORWARD cannot be detected without a server time source.</summary>
        public double ApplyOffline(DateTime utcNow)
        {
            long last = LastUpdateUtcTicks;
            LastUpdateUtcTicks = utcNow.Ticks;
            if (last == 0 || Paused || !OfflineProgressEnabled || utcNow.Ticks <= last) return 0;
            double hours = TimeSpan.FromTicks(utcNow.Ticks - last).TotalHours;
            if (hours > MaxOfflineHours) hours = MaxOfflineHours;
            VirtualDay += hours; return hours;
        }
    }
}
