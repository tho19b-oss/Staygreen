using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Kalender des <see cref="DateBox"/>, der sich im Editor unter dem Datumsfeld aufklappt (kein eigenes Fenster: wie
    /// alles in der App direkt an Ort und Stelle). Eine kleine Karte mit Monat und Jahr, Pfeilen zum Blaettern, den
    /// Wochentagen und sechs Wochen ab Montag. Der gewaehlte Tag ist gruen gefuellt, heute gruen umrandet, Tage anderer
    /// Monate sind blasser, Tage vor dem fruehesten erlaubten ausgegraut. Pfeiltasten wandern um einen Tag bzw. eine Woche,
    /// Bild hoch/runter um einen Monat (mit dem Fokus auch das Mausrad); Eingabe, Leertaste oder ein Klick waehlt den Tag,
    /// Esc klappt den Kalender ohne Aenderung zu.
    /// </summary>
    sealed class MonthView : Control, IMeasurable
    {
        // Masse in logischen Pixeln
        const int Pad = 10;
        const int HeaderHeight = 32;
        const int DayNamesHeight = 20;
        const int CellWidth = 36;
        const int CellHeight = 32;

        readonly DateEntry _rules;
        DateTime _month;          // erster Tag des gezeigten Monats
        DateTime _cursor;         // der gewaehlte Tag
        DateTime? _hoverDay;
        int _hoverNav;            // -1 = voriger Monat, +1 = naechster, 0 = keiner
        Font _titleFont;
        Font _selectedFont;

        public MonthView(DateEntry rules)
        {
            _rules = rules;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            AccessibleRole = AccessibleRole.Grouping;
            UpdateFonts();
        }

        /// <summary>Ein Tag wurde gewaehlt (Klick, Eingabe oder Leertaste).</summary>
        public event Action<DateTime> Picked;

        /// <summary>Esc: zuklappen ohne Aenderung.</summary>
        public event Action Dismissed;

        /// <summary>Zeigt den Monat von <paramref name="day"/> mit diesem Tag als gewaehltem.</summary>
        public void Reset(DateTime day)
        {
            _hoverDay = null;
            _hoverNav = 0;
            MoveCursor(day);
        }

        /// <summary>Die Hoehe steht fest; in der Breite fuellt die Karte nur ihren Teil, der Rest bleibt Hintergrund.</summary>
        public int MeasureHeight(int width)
        {
            return CardBounds.Height;
        }

        void MoveCursor(DateTime day)
        {
            day = _rules.AddDays(day, 0);   // in die Grenzen
            bool changed = day != _cursor;
            _cursor = day;
            _month = new DateTime(day.Year, day.Month, 1);
            Invalidate();
            if (changed) AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }

        void ShowMonth(int delta)
        {
            if (!CanShow(delta)) return;
            MoveCursor(_rules.AddMonths(_cursor, delta));
        }

        void Pick(DateTime day)
        {
            if (!_rules.IsSelectable(day)) return;
            MoveCursor(day);
            Action<DateTime> handler = Picked;
            if (handler != null) handler(day);
        }

        // ------------------------------------------------------------------ Aufteilung

        static Rectangle CardBounds
        {
            get
            {
                return new Rectangle(0, 0, Dpi.Px(2 * Pad + 7 * CellWidth),
                    Dpi.Px(2 * Pad + HeaderHeight + 4 + DayNamesHeight + 2 + DateEntry.Weeks * CellHeight));
            }
        }

        static Rectangle HeaderBounds
        {
            get { return new Rectangle(Dpi.Px(Pad), Dpi.Px(Pad), Dpi.Px(7 * CellWidth), Dpi.Px(HeaderHeight)); }
        }

        static Rectangle NavBounds(int direction)
        {
            Rectangle header = HeaderBounds;
            int size = header.Height;
            return new Rectangle(direction < 0 ? header.Left : header.Right - size, header.Top, size, size);
        }

        static int DayNamesTop
        {
            get { return Dpi.Px(Pad + HeaderHeight + 4); }
        }

        static Rectangle CellBounds(int index)
        {
            int top = DayNamesTop + Dpi.Px(DayNamesHeight + 2);
            int left = Dpi.Px(Pad);
            int col = index % 7;
            int row = index / 7;
            int x = left + Dpi.Px(col * CellWidth);
            int y = top + Dpi.Px(row * CellHeight);
            return new Rectangle(x, y, left + Dpi.Px((col + 1) * CellWidth) - x, top + Dpi.Px((row + 1) * CellHeight) - y);
        }

        DateTime? DayAt(Point point)
        {
            DateTime first = DateEntry.FirstCell(_month);
            for (int i = 0; i < 7 * DateEntry.Weeks; i++)
                if (CellBounds(i).Contains(point)) return first.AddDays(i);
            return null;
        }

        static int NavAt(Point point)
        {
            if (NavBounds(-1).Contains(point)) return -1;
            return NavBounds(1).Contains(point) ? 1 : 0;
        }

        bool CanShow(int direction)
        {
            return _rules.HasSelectableDays(_month.AddMonths(direction));
        }

        // ------------------------------------------------------------------ Tastatur und Maus

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        /// <summary>Eingabe und Esc gelten dem Kalender, nicht dem Editor darum (der wuerde sonst speichern bzw. zuklappen).</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                Pick(_cursor);
                return true;
            }
            if (keyData == Keys.Escape)
            {
                Action handler = Dismissed;
                if (handler != null) handler();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            switch (e.KeyData)
            {
                case Keys.Left:
                    MoveCursor(_rules.AddDays(_cursor, -1));
                    break;
                case Keys.Right:
                    MoveCursor(_rules.AddDays(_cursor, 1));
                    break;
                case Keys.Up:
                    MoveCursor(_rules.AddDays(_cursor, -7));
                    break;
                case Keys.Down:
                    MoveCursor(_rules.AddDays(_cursor, 7));
                    break;
                case Keys.PageUp:
                    ShowMonth(-1);
                    break;
                case Keys.PageDown:
                    ShowMonth(1);
                    break;
                case Keys.Space:
                    Pick(_cursor);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !CardBounds.Contains(e.Location)) return;
            Focus();
            int nav = NavAt(e.Location);
            if (nav != 0)
            {
                ShowMonth(nav);
                return;
            }
            DateTime? day = DayAt(e.Location);
            if (day.HasValue) Pick(day.Value);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            DateTime? day = DayAt(e.Location);
            int nav = NavAt(e.Location);
            if (day == _hoverDay && nav == _hoverNav) return;
            _hoverDay = day;
            _hoverNav = nav;
            Cursor = (day.HasValue && _rules.IsSelectable(day.Value)) || (nav != 0 && CanShow(nav)) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverDay = null;
            _hoverNav = 0;
            Invalidate();
        }

        /// <summary>
        /// Mit dem Fokus blaettert das Rad die Monate (vom Nutzer weg zurueck, zu ihm hin vor); sonst rollt wie gewohnt die
        /// Seite.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Focused || e.Delta == 0 || !CardBounds.Contains(e.Location)) return;
            ShowMonth(e.Delta > 0 ? -1 : 1);
            var handled = e as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        // ------------------------------------------------------------------ Zeichnen

        static CultureInfo Culture
        {
            get { return new CultureInfo(Loc.Language); }
        }

        string Title
        {
            get { return Culture.DateTimeFormat.GetMonthName(_month.Month) + " " + _month.Year.ToString(CultureInfo.InvariantCulture); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Background(g, this);
            Metrics.PaintRounded(g, CardBounds, Dpi.Px(10), Theme.Card, Focused ? Theme.Green : Theme.CardBorder);

            PaintNav(g, -1);
            PaintNav(g, 1);
            Rectangle header = HeaderBounds;
            int nav = header.Height;
            Draw.CenteredText(g, Title, _titleFont, new Rectangle(header.Left + nav, header.Top, header.Width - 2 * nav, header.Height), Theme.Text);

            int namesTop = DayNamesTop;
            for (int i = 0; i < 7; i++)
            {
                Rectangle cell = CellBounds(i);
                Draw.CenteredText(g, Loc.DayShort(i), Font, new Rectangle(cell.Left, namesTop, cell.Width, Dpi.Px(DayNamesHeight)), Theme.MutedStrong);
            }

            DateTime first = DateEntry.FirstCell(_month);
            DateTime today = DateTime.Today;
            int radius = Dpi.Px(6);
            for (int i = 0; i < 7 * DateEntry.Weeks; i++)
            {
                DateTime day = first.AddDays(i);
                Rectangle box = Rectangle.Inflate(CellBounds(i), -Dpi.Px(2), -Dpi.Px(2));
                bool selectable = _rules.IsSelectable(day);
                Color fore = !selectable ? Theme.Disabled : (day.Month == _month.Month ? Theme.Text : Theme.Muted);
                Font font = Font;
                if (day == _cursor)
                {
                    Metrics.PaintRounded(g, box, radius, Theme.GreenButton, Color.Empty);
                    fore = Theme.OnAccent;
                    font = _selectedFont;
                }
                else
                {
                    if (selectable && day == _hoverDay) Metrics.PaintRounded(g, box, radius, Theme.Chip, Color.Empty);
                    if (day == today) Metrics.PaintRounded(g, box, radius, Color.Empty, Theme.Green);
                }
                Draw.CenteredText(g, day.Day.ToString(CultureInfo.InvariantCulture), font, box, fore);
            }
        }

        void PaintNav(Graphics g, int direction)
        {
            Rectangle bounds = NavBounds(direction);
            bool enabled = CanShow(direction);
            if (enabled && _hoverNav == direction)
                Metrics.PaintRounded(g, bounds, Dpi.Px(8), Theme.Chip, Color.Empty);

            Color color = !enabled ? Theme.Disabled : (_hoverNav == direction ? Theme.Text : Theme.Muted);
            float x = bounds.Left + bounds.Width / 2f;
            float y = bounds.Top + bounds.Height / 2f;
            float half = 4.5f * Dpi.Factor;
            float depth = 2.25f * Dpi.Factor * direction;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, Math.Max(1.25f, 1.6f * Dpi.Factor)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new[] { new PointF(x - depth, y - half), new PointF(x + depth, y), new PointF(x - depth, y + half) });
            }
        }

        // ------------------------------------------------------------------ Schrift und Screenreader

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateFonts();
            Invalidate();
        }

        void UpdateFonts()
        {
            Font oldTitle = _titleFont;
            Font oldSelected = _selectedFont;
            _titleFont = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold);
            _selectedFont = Fonts.Semibold(Font, 0f);
            if (oldTitle != null) oldTitle.Dispose();
            if (oldSelected != null) oldSelected.Dispose();
        }

        /// <summary>Der gewaehlte Tag.</summary>
        DateTime SelectedDay
        {
            get { return _cursor; }
        }

        /// <summary>Screenreader lesen den gewaehlten Tag ausgeschrieben ("Freitag, 9. Oktober 2026").</summary>
        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new MonthViewAccessibleObject(this);
        }

        sealed class MonthViewAccessibleObject : ControlAccessibleObject
        {
            readonly MonthView _owner;

            public MonthViewAccessibleObject(MonthView owner)
                : base(owner)
            {
                _owner = owner;
            }

            public override string Value
            {
                get { return _owner.SelectedDay.ToString("D", Culture); }
                set { }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_titleFont != null) _titleFont.Dispose();
                if (_selectedFont != null) _selectedFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
