using System;
using System.Collections.Generic;
using System.Globalization;

namespace StayGreen.Core
{
    /// <summary>
    /// Ein Zeitraum aus ganzen Tagen (Urlaub, Feiertag), beide Grenzen eingeschlossen. Die Uhrzeit wird ignoriert.
    /// An diesen Tagen beginnt kein Zeitfenster des Zeitplans (siehe <see cref="Scheduler"/>).
    /// </summary>
    public sealed class DateRange
    {
        const string DateFormat = "yyyy-MM-dd";

        public DateRange()
        {
        }

        public DateRange(DateTime from, DateTime to)
        {
            From = from.Date;
            To = to.Date;
        }

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public bool IsValid
        {
            get { return From != DateTime.MinValue && To != DateTime.MinValue && To.Date >= From.Date; }
        }

        /// <summary>Anzahl der Tage (beide Grenzen eingeschlossen); 0 bei ungueltigem Zeitraum.</summary>
        public int DayCount
        {
            get { return IsValid ? (int)(To.Date - From.Date).TotalDays + 1 : 0; }
        }

        public bool Contains(DateTime day)
        {
            DateTime d = day.Date;
            return d >= From.Date && d <= To.Date;
        }

        public DateRange Clone()
        {
            return new DateRange(From, To);
        }

        /// <summary>Dateiformat: "2026-12-24;2026-12-31".</summary>
        public string Serialize()
        {
            return From.ToString(DateFormat, CultureInfo.InvariantCulture) + ";"
                   + To.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        public static bool TryParse(string text, out DateRange range)
        {
            range = null;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Trim().Split(';');
            if (parts.Length != 2) return false;
            DateTime from, to;
            if (!DateTime.TryParseExact(parts[0].Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out from))
                return false;
            if (!DateTime.TryParseExact(parts[1].Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out to))
                return false;
            range = new DateRange(from, to);
            return true;
        }

        public override string ToString()
        {
            return Serialize();
        }

        // ------------------------------------------------------------------ Listen von Zeitraeumen

        public static bool ContainsDay(IList<DateRange> list, DateTime day)
        {
            if (list == null) return false;
            foreach (DateRange r in list)
                if (r != null && r.Contains(day)) return true;
            return false;
        }

        /// <summary>
        /// Bringt die Liste in Ordnung: vertauschte Grenzen werden gedreht, ungueltige Eintraege entfallen,
        /// ueberlappende oder direkt aneinander grenzende Zeitraeume werden zusammengefasst, die Liste ist
        /// nach Datum sortiert und auf <see cref="Settings.MaxExceptions"/> Eintraege begrenzt.
        /// </summary>
        public static void Normalize(List<DateRange> list)
        {
            if (list == null) return;

            var fixedRanges = new List<DateRange>();
            foreach (DateRange r in list)
            {
                if (r == null || r.From == DateTime.MinValue || r.To == DateTime.MinValue) continue;
                fixedRanges.Add(r.To.Date < r.From.Date ? new DateRange(r.To, r.From) : new DateRange(r.From, r.To));
            }
            fixedRanges.Sort((a, b) => a.From.CompareTo(b.From));

            var merged = new List<DateRange>();
            foreach (DateRange r in fixedRanges)
            {
                DateRange last = merged.Count == 0 ? null : merged[merged.Count - 1];
                if (last != null && r.From.Date <= last.To.Date.AddDays(1))
                {
                    if (r.To.Date > last.To.Date) last.To = r.To.Date;
                }
                else
                {
                    merged.Add(r);
                }
            }

            if (merged.Count > Settings.MaxExceptions) merged.RemoveRange(Settings.MaxExceptions, merged.Count - Settings.MaxExceptions);
            list.Clear();
            list.AddRange(merged);
        }

        /// <summary>Nimmt einen einzelnen Tag in die Ausnahmen auf (fasst mit Nachbarn zusammen).</summary>
        public static void Exclude(List<DateRange> list, DateTime day)
        {
            if (list == null) return;
            list.Add(new DateRange(day, day));
            Normalize(list);
        }

        /// <summary>Nimmt einen einzelnen Tag wieder aus den Ausnahmen heraus (ein Zeitraum wird dabei ggf. geteilt).</summary>
        public static void Include(List<DateRange> list, DateTime day)
        {
            if (list == null) return;
            DateTime d = day.Date;
            var result = new List<DateRange>();
            foreach (DateRange r in list)
            {
                if (r == null) continue;
                if (!r.Contains(d))
                {
                    result.Add(r);
                    continue;
                }
                if (r.From.Date < d) result.Add(new DateRange(r.From, d.AddDays(-1)));
                if (r.To.Date > d) result.Add(new DateRange(d.AddDays(1), r.To));
            }
            list.Clear();
            list.AddRange(result);
        }

        /// <summary>
        /// Entfernt Zeitraeume, die laenger als einen Tag vorbei sind. Einen Tag Puffer braucht der Zeitplan,
        /// weil ein Fenster ueber Mitternacht erst am Folgetag endet.
        /// </summary>
        public static void Prune(List<DateRange> list, DateTime today)
        {
            if (list == null) return;
            DateTime limit = today.Date.AddDays(-1);
            list.RemoveAll(r => r == null || r.To.Date < limit);
        }
    }
}
