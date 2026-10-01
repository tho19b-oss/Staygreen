using System;

namespace StayGreen.Core
{
    /// <summary>Berechnet, wann der Auto-Stopp als Naechstes faellig ist.</summary>
    public static class AutoStopPlanner
    {
        /// <summary>
        /// Ein Termin, der schon laenger als so viele Sekunden zurueckliegt (Rechner war im Standby, Uhr wurde
        /// gestellt), wird NICHT mehr nachgeholt. Sonst wuerde der Rechner am naechsten Morgen beim Aufklappen
        /// sofort herunterfahren.
        /// </summary>
        public const int MissedToleranceSeconds = 120;

        /// <summary>Naechster Termin nach <paramref name="now"/>; null, wenn der Auto-Stopp aus oder abgelaufen ist.</summary>
        public static DateTime? NextDue(Settings settings, DateTime now)
        {
            if (settings == null || !settings.AutoStopEnabled) return null;

            if (settings.AutoStopTiming == StopTiming.Once)
            {
                if (settings.AutoStopOnce != DateTime.MinValue && settings.AutoStopOnce > now)
                    return settings.AutoStopOnce;
                return null;
            }

            DateTime candidate = now.Date + settings.AutoStopTime;
            if (candidate <= now) candidate = candidate.AddDays(1);
            return candidate;
        }

        public static bool IsMissed(DateTime due, DateTime now)
        {
            return (now - due).TotalSeconds > MissedToleranceSeconds;
        }
    }
}
