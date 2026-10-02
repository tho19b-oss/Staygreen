using System;
using System.Globalization;

namespace StayGreen.Core
{
    /// <summary>
    /// Ein Zeitfenster: an den markierten Wochentagen von <see cref="Start"/> bis <see cref="End"/>.
    /// Liegt das Ende vor dem Start, geht das Fenster ueber Mitternacht (22:00-06:00).
    /// Start == Ende bedeutet "leer" und wird ignoriert.
    /// </summary>
    public sealed class ScheduleRule
    {
        static readonly bool[] WeekdayMask = { true, true, true, true, true, false, false };

        /// <summary>Index 0 = Montag ... 6 = Sonntag.</summary>
        public bool[] Days { get; } = new bool[7];

        public TimeSpan Start { get; set; }

        public TimeSpan End { get; set; }

        public ScheduleRule()
        {
        }

        public ScheduleRule(bool[] days, TimeSpan start, TimeSpan end)
        {
            if (days != null)
                for (int i = 0; i < 7 && i < days.Length; i++)
                    Days[i] = days[i];
            Start = start;
            End = end;
        }

        /// <summary>Montag-Freitag, <paramref name="from"/> bis <paramref name="to"/>.</summary>
        public static ScheduleRule Weekdays(TimeSpan from, TimeSpan to)
        {
            return new ScheduleRule(WeekdayMask, from, to);
        }

        public static int DayIndex(DayOfWeek day)
        {
            return ((int)day + 6) % 7; // Montag = 0, Sonntag = 6
        }

        public bool Covers(DayOfWeek day)
        {
            return Days[DayIndex(day)];
        }

        public bool AnyDay
        {
            get
            {
                foreach (bool d in Days)
                    if (d) return true;
                return false;
            }
        }

        public bool CrossesMidnight
        {
            get { return End < Start; }
        }

        /// <summary>Ein Fenster ohne Tage oder mit Start == Ende wird nie aktiv.</summary>
        public bool IsUsable
        {
            get { return AnyDay && Start != End; }
        }

        public ScheduleRule Clone()
        {
            return new ScheduleRule(Days, Start, End);
        }

        public static string FormatTime(TimeSpan t)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", t.Hours, t.Minutes);
        }

        public static bool TryParseTime(string text, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Trim().Split(':');
            if (parts.Length != 2) return false;
            int h, m;
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out h)) return false;
            if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out m)) return false;
            if (h < 0 || h > 23 || m < 0 || m > 59) return false;
            time = new TimeSpan(h, m, 0);
            return true;
        }

        /// <summary>Dateiformat: "1111100;08:00;17:00" (Mo..So als 0/1).</summary>
        public string Serialize()
        {
            var mask = new char[7];
            for (int i = 0; i < 7; i++) mask[i] = Days[i] ? '1' : '0';
            return new string(mask) + ";" + FormatTime(Start) + ";" + FormatTime(End);
        }

        public static bool TryParse(string text, out ScheduleRule rule)
        {
            rule = null;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Trim().Split(';');
            if (parts.Length != 3 || parts[0].Length != 7) return false;
            var days = new bool[7];
            for (int i = 0; i < 7; i++)
            {
                if (parts[0][i] == '1') days[i] = true;
                else if (parts[0][i] != '0') return false;
            }
            TimeSpan start, end;
            if (!TryParseTime(parts[1], out start) || !TryParseTime(parts[2], out end)) return false;
            rule = new ScheduleRule(days, start, end);
            return true;
        }

        public override string ToString()
        {
            return Serialize();
        }
    }
}
