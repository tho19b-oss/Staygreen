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

        /// <summary>
        /// Im Modus "Am Zeitplanende" zaehlt nur ein Fensterende als Feierabend, nach dem mindestens so lange
        /// kein Fenster mehr beginnt. Kuerzere Luecken (Mittagspause) loesen den Auto-Stopp nicht aus.
        /// </summary>
        public static readonly TimeSpan EndOfDayGap = TimeSpan.FromHours(3);

        const int MaxSteps = 64;

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

            if (settings.AutoStopTiming == StopTiming.ScheduleEnd)
                return NextScheduleEnd(settings, now);

            DateTime candidate = now.Date + settings.AutoStopTime;
            if (candidate <= now) candidate = candidate.AddDays(1);
            return candidate;
        }

        /// <summary>
        /// Ende des naechsten "Arbeitstags": das erste Fensterende, auf das fuer mindestens <see cref="EndOfDayGap"/>
        /// kein neues Fenster folgt. Null ohne wirksamen Zeitplan.
        /// </summary>
        public static DateTime? NextScheduleEnd(Settings settings, DateTime now)
        {
            if (settings == null || !settings.ScheduleActive) return null;

            DateTime cursor = now;
            for (int step = 0; step < MaxSteps; step++)
            {
                DateTime? change = Scheduler.NextChange(cursor, settings.Rules);
                if (!change.HasValue) return null;

                // Ein Fenster beginnt: weiter zu seinem Ende.
                if (Scheduler.IsActive(change.Value, settings.Rules))
                {
                    cursor = change.Value;
                    continue;
                }

                // Ein Fenster endet. Folgt bald das naechste, ist es nur eine Pause (z. B. Mittag).
                DateTime? next = Scheduler.NextChange(change.Value, settings.Rules);
                if (!next.HasValue || next.Value - change.Value >= EndOfDayGap) return change.Value;
                cursor = next.Value;
            }
            return null;
        }

        public static bool IsMissed(DateTime due, DateTime now)
        {
            return (now - due).TotalSeconds > MissedToleranceSeconds;
        }
    }
}
