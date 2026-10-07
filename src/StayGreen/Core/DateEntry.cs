using System;

namespace StayGreen.Core
{
    /// <summary>
    /// Rechenregeln des Datumsfelds ohne Oberflaeche (damit sie sich testen lassen): ein Tag zwischen <see cref="Minimum"/>
    /// und <see cref="MaxSupported"/>, der sich tage- oder monatsweise weiterblaettern laesst, und das Monatsblatt des
    /// Kalenders (sechs Wochen, beginnend mit Montag wie die Wochentage im Zeitplan).
    /// </summary>
    public sealed class DateEntry
    {
        /// <summary>Fruehester Tag, den das Feld ueberhaupt annimmt (wie beim Windows-Datumsfeld).</summary>
        public static readonly DateTime MinSupported = new DateTime(1753, 1, 1);

        /// <summary>Spaetester Tag, den das Feld annimmt (wie beim Windows-Datumsfeld).</summary>
        public static readonly DateTime MaxSupported = new DateTime(9998, 12, 31);

        /// <summary>Ein Monatsblatt zeigt immer sechs Wochen, damit der Kalender beim Blaettern nicht in der Hoehe springt.</summary>
        public const int Weeks = 6;

        DateTime _value = DateTime.Today;
        DateTime _minimum = MinSupported;

        /// <summary>Fruehester waehlbarer Tag; ein frueherer Wert rueckt darauf.</summary>
        public DateTime Minimum
        {
            get { return _minimum; }
            set
            {
                _minimum = Clamp(value.Date, MinSupported, MaxSupported);
                _value = Clamp(_value, _minimum, MaxSupported);
            }
        }

        /// <summary>Der gewaehlte Tag (ohne Uhrzeit).</summary>
        public DateTime Value
        {
            get { return _value; }
        }

        /// <summary>Setzt den Tag; die Uhrzeit faellt weg, ausserhalb der Grenzen rueckt er an den Rand.</summary>
        public void SetValue(DateTime date)
        {
            _value = Clamp(date.Date, _minimum, MaxSupported);
        }

        public void AddDays(int days)
        {
            _value = AddDays(_value, days);
        }

        /// <summary>Ob <see cref="AddDays(int)"/> den Tag aendern wuerde (am Rand nicht).</summary>
        public bool CanAddDays(int days)
        {
            return AddDays(_value, days) != _value;
        }

        public bool IsSelectable(DateTime day)
        {
            return day.Date >= _minimum && day.Date <= MaxSupported;
        }

        /// <summary>Hat der Monat von <paramref name="month"/> mindestens einen waehlbaren Tag?</summary>
        public bool HasSelectableDays(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            DateTime last = first.AddDays(DateTime.DaysInMonth(month.Year, month.Month) - 1);
            return last >= _minimum && first <= MaxSupported;
        }

        /// <summary><paramref name="date"/> um <paramref name="days"/> Tage verschoben, in den Grenzen (ohne Ueberlauf).</summary>
        public DateTime AddDays(DateTime date, int days)
        {
            date = Clamp(date.Date, _minimum, MaxSupported);
            double target = (date - _minimum).TotalDays + days;
            if (target <= 0) return _minimum;
            if (target >= (MaxSupported - _minimum).TotalDays) return MaxSupported;
            return _minimum.AddDays(target);
        }

        /// <summary>
        /// <paramref name="date"/> um <paramref name="months"/> Monate verschoben, in den Grenzen. Gibt es den Tag im Zielmonat
        /// nicht (31. Februar), wird es dessen letzter Tag.
        /// </summary>
        public DateTime AddMonths(DateTime date, int months)
        {
            date = Clamp(date.Date, _minimum, MaxSupported);
            long index = date.Year * 12L + (date.Month - 1) + months;
            index = Math.Min(Math.Max(index, MinSupported.Year * 12L), MaxSupported.Year * 12L + 11);
            int year = (int)(index / 12);
            int month = (int)(index % 12) + 1;
            int day = Math.Min(date.Day, DateTime.DaysInMonth(year, month));
            return Clamp(new DateTime(year, month, day), _minimum, MaxSupported);
        }

        /// <summary>Erster Tag des Monatsblatts: der Montag an oder vor dem Ersten des Monats.</summary>
        public static DateTime FirstCell(DateTime month)
        {
            var first = new DateTime(month.Year, month.Month, 1);
            return first.AddDays(-ScheduleRule.DayIndex(first.DayOfWeek));
        }

        static DateTime Clamp(DateTime value, DateTime min, DateTime max)
        {
            return value < min ? min : (value > max ? max : value);
        }
    }
}
