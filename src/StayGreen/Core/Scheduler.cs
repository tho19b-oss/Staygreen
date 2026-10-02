using System;
using System.Collections.Generic;

namespace StayGreen.Core
{
    /// <summary>Wertet die Zeitfenster aus (rein funktional, ohne Systemuhr, daher testbar).</summary>
    public static class Scheduler
    {
        /// <summary>So weit (in Tagen) wird nach dem naechsten Wechsel gesucht, damit auch lange Urlaube gefunden werden.</summary>
        public const int HorizonDays = 400;

        /// <summary>Die Suche laeuft in Etappen dieser Laenge (Tage), damit der Normalfall schnell bleibt.</summary>
        const int ChunkDays = 8;

        /// <summary>Liegt <paramref name="now"/> in mindestens einem Zeitfenster?</summary>
        public static bool IsActive(DateTime now, IList<ScheduleRule> rules)
        {
            return IsActive(now, rules, null);
        }

        /// <summary>
        /// Liegt <paramref name="now"/> in mindestens einem Zeitfenster? Ein Fenster gehoert zu dem Tag, an dem es
        /// beginnt. Beginnt es an einem Ausnahmetag, entfaellt es ganz (auch sein Teil nach Mitternacht).
        /// </summary>
        public static bool IsActive(DateTime now, IList<ScheduleRule> rules, IList<DateRange> exceptions)
        {
            if (rules == null) return false;
            foreach (ScheduleRule rule in rules)
                if (RuleActive(now, rule, exceptions)) return true;
            return false;
        }

        public static bool IsExcluded(DateTime day, IList<DateRange> exceptions)
        {
            return DateRange.ContainsDay(exceptions, day);
        }

        static bool RuleActive(DateTime now, ScheduleRule rule, IList<DateRange> exceptions)
        {
            if (rule == null || !rule.IsUsable) return false;
            TimeSpan t = now.TimeOfDay;

            if (!rule.CrossesMidnight)
                return rule.Covers(now.DayOfWeek) && !IsExcluded(now, exceptions) && t >= rule.Start && t < rule.End;

            // Fenster ueber Mitternacht: abends am markierten Tag, morgens am Folgetag.
            if (rule.Covers(now.DayOfWeek) && !IsExcluded(now, exceptions) && t >= rule.Start) return true;
            DateTime yesterday = now.AddDays(-1);
            return rule.Covers(yesterday.DayOfWeek) && !IsExcluded(yesterday, exceptions) && t < rule.End;
        }

        /// <summary>
        /// Naechster Zeitpunkt nach <paramref name="now"/>, an dem sich aendert, ob die Zeitfenster aktiv sind
        /// (Start oder Ende). Benachbarte/ueberlappende Fenster werden korrekt zusammengefasst.
        /// Null, wenn sich innerhalb von <see cref="HorizonDays"/> Tagen nichts aendert.
        /// </summary>
        public static DateTime? NextChange(DateTime now, IList<ScheduleRule> rules)
        {
            return NextChange(now, rules, null);
        }

        public static DateTime? NextChange(DateTime now, IList<ScheduleRule> rules, IList<DateRange> exceptions)
        {
            if (rules == null || rules.Count == 0) return null;

            bool anyUsable = false;
            foreach (ScheduleRule rule in rules)
                if (rule != null && rule.IsUsable) anyUsable = true;
            if (!anyUsable) return null;

            bool current = IsActive(now, rules, exceptions);
            DateTime today = now.Date;

            for (int first = 0; first < HorizonDays; first += ChunkDays)
            {
                // Diese Etappe liefert alle Wechsel im Zeitraum [chunkStart, chunkEnd). Ein Tag davor wird mit
                // betrachtet, weil ein Fenster ueber Mitternacht, das dort begann, erst in dieser Etappe endet.
                DateTime chunkStart = today.AddDays(first);
                DateTime chunkEnd = chunkStart.AddDays(ChunkDays);

                var candidates = new List<DateTime>();
                for (int offset = first - 1; offset < first + ChunkDays; offset++)
                {
                    DateTime day = today.AddDays(offset);
                    foreach (ScheduleRule rule in rules)
                    {
                        if (rule == null || !rule.IsUsable || !rule.Covers(day.DayOfWeek) || IsExcluded(day, exceptions)) continue;
                        AddCandidate(candidates, day + rule.Start, now, chunkStart, chunkEnd);
                        AddCandidate(candidates, rule.CrossesMidnight ? day.AddDays(1) + rule.End : day + rule.End,
                            now, chunkStart, chunkEnd);
                    }
                }

                candidates.Sort();
                foreach (DateTime candidate in candidates)
                    if (IsActive(candidate, rules, exceptions) != current)
                        return candidate;
            }
            return null;
        }

        static void AddCandidate(List<DateTime> candidates, DateTime value, DateTime now, DateTime chunkStart, DateTime chunkEnd)
        {
            if (value > now && value >= chunkStart && value < chunkEnd) candidates.Add(value);
        }
    }
}
