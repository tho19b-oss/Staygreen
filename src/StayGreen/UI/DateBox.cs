using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Datumsfeld im Stil der Karten (Auto-Stopp "Einmalig"). Ersetzt das Windows-Datumsfeld, das im dunklen Design hell
    /// blieb. Zeigt ein Kalendersymbol, den Wochentag und das Datum ("Fr 09.10.2026", englisch "Fri Oct 9, 2026"). Ein
    /// Klick darauf, die Leertaste, F4 oder Alt+Pfeil runter klappt den <see cref="Calendar"/> auf oder zu; die Seite setzt
    /// ihn unter das Feld. Pfeil hoch/runter, die kleinen Pfeile rechts und das Mausrad blaettern einen Tag weiter. Tage vor
    /// <see cref="Minimum"/> lassen sich nicht waehlen.
    /// </summary>
    sealed class DateBox : SpinField
    {
        /// <summary>Abstand zwischen Kalendersymbol und Datum in logischen Pixeln.</summary>
        const int IconGap = 6;

        readonly DateEntry _entry = new DateEntry();
        bool _calendarOpen;

        public DateBox()
        {
            Calendar = new MonthView(_entry) { Visible = false };
            Calendar.Picked += day =>
            {
                Value = day;
                CloseCalendar();
            };
            Calendar.Dismissed += CloseCalendar;
            Fit();
        }

        public event EventHandler ValueChanged;

        /// <summary>Der Kalender wurde auf- oder zugeklappt: Die Seite ordnet sich neu an.</summary>
        public event EventHandler CalendarToggled;

        /// <summary>Der Kalender zum Feld; die Seite setzt ihn in den Editor unter das Feld.</summary>
        public MonthView Calendar { get; private set; }

        public bool CalendarOpen
        {
            get { return _calendarOpen; }
        }

        /// <summary>Der Tag (ohne Uhrzeit); ein Wert vor <see cref="Minimum"/> rueckt darauf.</summary>
        public DateTime Value
        {
            get { return _entry.Value; }
            set { Change(() => _entry.SetValue(value)); }
        }

        /// <summary>Fruehester waehlbarer Tag.</summary>
        public DateTime Minimum
        {
            get { return _entry.Minimum; }
            set { Change(() => _entry.Minimum = value); }
        }

        /// <summary>Nach einem Sprachwechsel: Schreibweise und Breite neu.</summary>
        public void ApplyTexts()
        {
            Fit();
            Invalidate();
            Calendar.Invalidate();
        }

        /// <summary>"Fr 09.10.2026", englisch "Fri Oct 9, 2026".</summary>
        static string Format(DateTime date)
        {
            string day = Loc.DayShort(ScheduleRule.DayIndex(date.DayOfWeek));
            string text = Loc.Language == "en"
                ? date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                : date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            return day + " " + text;
        }

        void Change(Action action)
        {
            DateTime before = _entry.Value;
            action();
            Invalidate();
            if (_entry.Value == before) return;
            if (_calendarOpen) Calendar.Reset(_entry.Value);
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            EventHandler handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        protected override void Step(int delta)
        {
            Change(() => _entry.AddDays(delta));
        }

        protected override bool CanStep(int delta)
        {
            return _entry.CanAddDays(delta);
        }

        /// <summary>Gruener Rand auch, solange der Kalender offen ist: Er gehoert zu diesem Feld.</summary>
        protected override bool ShowsFocus
        {
            get { return Focused || _calendarOpen; }
        }

        // ------------------------------------------------------------------ Kalender

        /// <summary>Klappt den Kalender auf, mit dem Tag des Felds gewaehlt, und gibt ihm den Fokus.</summary>
        public void OpenCalendar()
        {
            if (_calendarOpen || !Visible) return;
            _calendarOpen = true;
            Calendar.AccessibleName = AccessibleName;
            Calendar.Reset(_entry.Value);
            Calendar.Visible = true;
            OnCalendarToggled();
            Calendar.Focus();
            Invalidate();
        }

        /// <summary>
        /// Klappt den Kalender zu; hatte er den Fokus, geht er zurueck an das Feld. Die Seite ruft das auch, wenn sie das
        /// Feld ausblendet (andere Art gewaehlt, Editor neu geoeffnet).
        /// </summary>
        public void CloseCalendar()
        {
            if (!_calendarOpen) return;
            _calendarOpen = false;
            if (Calendar.ContainsFocus) Focus();
            Calendar.Visible = false;
            OnCalendarToggled();
            Invalidate();
        }

        void ToggleCalendar()
        {
            if (_calendarOpen) CloseCalendar();
            else OpenCalendar();
        }

        void OnCalendarToggled()
        {
            EventHandler handler = CalendarToggled;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------------ Tastatur und Maus

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            switch (e.KeyData)
            {
                case Keys.Up:
                    Step(1);
                    break;
                case Keys.Down:
                    Step(-1);
                    break;
                case Keys.Space:
                case Keys.F4:
                case Keys.Alt | Keys.Down:
                    ToggleCalendar();
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        protected override void OnContentClick(Point location)
        {
            ToggleCalendar();
        }

        // ------------------------------------------------------------------ Groesse und Zeichnen

        /// <summary>Die breiteste Schreibweise (jeder Wochentag in jedem Monat), damit das Feld beim Blaettern nicht springt.</summary>
        protected override int ContentWidth
        {
            get
            {
                int widest = 0;
                for (int month = 1; month <= 12; month++)
                    for (int day = 22; day <= 28; day++)
                        widest = Math.Max(widest, Metrics.TextWidth(Format(new DateTime(2028, month, day)), Font));
                return Dpi.Px(IconPainter.Size + IconGap) + widest;
            }
        }

        protected override void PaintContent(Graphics g, Color fore)
        {
            int icon = Dpi.Px(IconPainter.Size);
            IconPainter.Draw(g, IconKind.Calendar, new Rectangle(TextLeft, (Height - icon) / 2, icon, icon),
                Enabled ? Theme.MutedStrong : Theme.Disabled);

            string text = Format(_entry.Value);
            int left = TextLeft + icon + Dpi.Px(IconGap);
            if (Focused)
            {
                // Markiert wie in den anderen Feldern: Darauf wirken die Pfeiltasten.
                int width = Metrics.TextWidth(text, Font);
                int height = Font.Height + Dpi.Px(2);
                var mark = new Rectangle(left - Dpi.Px(2), (Height - height) / 2, width + Dpi.Px(4), height);
                Metrics.PaintRounded(g, mark, Dpi.Px(3), Theme.GreenButton, Color.Empty);
                fore = Theme.OnAccent;
            }
            TextRenderer.DrawText(g, text, Font, new Rectangle(left, 0, Math.Max(1, ArrowBounds.Left - left), Height), fore, TextFlags);
        }

        // ------------------------------------------------------------------ Screenreader

        /// <summary>Screenreader lesen das Datum ausgeschrieben ("Freitag, 9. Oktober 2026") und koennen es auch setzen.</summary>
        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new DateBoxAccessibleObject(this);
        }

        sealed class DateBoxAccessibleObject : ControlAccessibleObject
        {
            readonly DateBox _owner;

            public DateBoxAccessibleObject(DateBox owner)
                : base(owner)
            {
                _owner = owner;
            }

            public override string Value
            {
                get { return _owner.Value.ToString("D", new CultureInfo(Loc.Language)); }
                set
                {
                    DateTime date;
                    if (DateTime.TryParse(value, new CultureInfo(Loc.Language), DateTimeStyles.None, out date)) _owner.Value = date;
                }
            }
        }
    }
}
