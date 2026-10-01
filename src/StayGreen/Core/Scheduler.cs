using System;
using System.Collections.Generic;

namespace StayGreen.Core
{
    /// <summary>Wertet die Zeitfenster aus (rein funktional, ohne Systemuhr, daher testbar).</summary>
    public static class Scheduler
    {
        /// <summary>Liegt <paramref name="now"/> in mindestens einem Zeitfenster?</summary>
        public static bool IsActive(DateTime now, IList<ScheduleRule> rules)
        {
            if (rules == null) return false;
            foreach (ScheduleRule rule in rules)
                if (RuleActive(now, rule)) return true;
            return false;
        }

        static bool RuleActive(DateTime now, ScheduleRule rule)
        {
            if (rule == null || !rule.IsUsable) return false;
            TimeSpan t = now.TimeOfDay;

            if (!rule.CrossesMidnight)
                return rule.Covers(now.DayOfWeek) && t >= rule.Start && t < rule.End;

            // Fenster ueber Mitternacht: abends am markierten Tag, morgens am Folgetag.
            if (rule.Covers(now.DayOfWeek) && t >= rule.Start) return true;
            return rule.Covers(now.AddDays(-1).DayOfWeek) && t < rule.End;
        }

        /// <summary>
        /// Naechster Zeitpunkt nach <paramref name="now"/>, an dem sich aendert, ob die Zeitfenster aktiv sind
        /// (Start oder Ende). Benachbarte/ueberlappende Fenster werden korrekt zusammengefasst.
        /// Null, wenn sich in den naechsten Tagen nichts aendert.
        /// </summary>
        public static DateTime? NextChange(DateTime now, IList<ScheduleRule> rules)
        {
            if (rules == null || rules.Count == 0) return null;

            bool current = IsActive(now, rules);
            var candidates = new List<DateTime>();
            for (int offset = 0; offset <= 8; offset++)
            {
                DateTime day = now.Date.AddDays(offset);
                foreach (ScheduleRule rule in rules)
                {
                    if (rule == null || !rule.IsUsable || !rule.Covers(day.DayOfWeek)) continue;
                    candidates.Add(day + rule.Start);
                    candidates.Add(rule.CrossesMidnight ? day.AddDays(1) + rule.End : day + rule.End);
                }
            }

            candidates.Sort();
            foreach (DateTime candidate in candidates)
                if (candidate > now && IsActive(candidate, rules) != current)
                    return candidate;
            return null;
        }
    }
}
