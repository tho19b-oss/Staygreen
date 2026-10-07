using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Uhrzeitfeld "HH:mm" (24 Stunden) im Stil der Karten. Ersetzt das Windows-Uhrzeitfeld, das sich im dunklen Design
    /// nicht einfaerben laesst. Stunden und Minuten sind zwei Teile: Ziffern tippen, Pfeil hoch/runter oder die kleinen
    /// Pfeile rechts aendern den gewaehlten Teil, Pfeil links/rechts (oder ":") wechselt ihn. Das Mausrad aendert den Wert
    /// nur, solange das Feld den Fokus hat; sonst rollt wie gewohnt die Seite.
    /// </summary>
    sealed class TimeBox : Control
    {
        const int HourPart = 0;
        const int MinutePart = 1;

        readonly Timer _repeat = new Timer();
        int _hours;
        int _minutes;
        int _part = HourPart;
        int _pending = -1;     // erste Ziffer einer zweistelligen Eingabe, -1 = keine
        int _hoverArrow;       // +1 = oberer Pfeil, -1 = unterer, 0 = keiner
        int _pressedArrow;
        bool _compact;

        public TimeBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            AccessibleRole = AccessibleRole.SpinButton;

            // Gedrueckt gehaltener Pfeil: nach einer kurzen Pause schnell weiterzaehlen (wie beim Windows-Feld).
            _repeat.Tick += (o, e) =>
            {
                _repeat.Interval = 70;
                Step(_pressedArrow);
            };
            Fit();
        }

        public event EventHandler ValueChanged;

        /// <summary>
        /// Flach wie die Windows-Eingabefelder, z. B. neben einem Datums- oder Zahlenfeld; sonst so hoch wie die runden
        /// Wochentag-Knoepfe im Zeitplan.
        /// </summary>
        public bool Compact
        {
            get { return _compact; }
            set
            {
                if (_compact == value) return;
                _compact = value;
                Fit();
            }
        }

        /// <summary>Die Uhrzeit; nur Stunden und Minuten zaehlen (Sekunden und ganze Tage fallen weg).</summary>
        public TimeSpan Value
        {
            get { return new TimeSpan(_hours, _minutes, 0); }
            set
            {
                int total = (int)Math.Floor(value.TotalMinutes) % (24 * 60);
                if (total < 0) total += 24 * 60;
                Set(total / 60, total % 60);
            }
        }

        void Set(int hours, int minutes)
        {
            if (hours == _hours && minutes == _minutes) return;
            _hours = hours;
            _minutes = minutes;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            EventHandler handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        void SelectPart(int part)
        {
            _pending = -1;
            if (_part == part) return;
            _part = part;
            Invalidate();
        }

        /// <summary>Gewaehlten Teil um <paramref name="delta"/> weiterdrehen (23 -> 00, 59 -> 00, ohne Uebertrag).</summary>
        void Step(int delta)
        {
            if (delta == 0) return;
            _pending = -1;
            if (_part == HourPart) Set((_hours + delta + 24) % 24, _minutes);
            else Set(_hours, (_minutes + delta + 60) % 60);
        }

        /// <summary>
        /// Eine getippte Ziffer: Die erste ersetzt den Teil, die zweite ergaenzt sie. Nach vollstaendigen Stunden geht es zu
        /// den Minuten ("8" oder "08" -> 08 und weiter; "2" wartet auf eine zweite Ziffer).
        /// </summary>
        void Type(int digit)
        {
            if (_part == HourPart)
            {
                if (_pending < 0)
                {
                    Set(digit, _minutes);
                    if (digit > 2) _part = MinutePart;
                    else _pending = digit;
                }
                else
                {
                    int hours = _pending * 10 + digit;
                    _pending = -1;
                    if (hours > 23)
                    {
                        Type(digit);   // "25": die 5 gilt als neue erste Ziffer
                        return;
                    }
                    Set(hours, _minutes);
                    _part = MinutePart;
                }
            }
            else if (_pending < 0)
            {
                Set(_hours, digit);
                if (digit <= 5) _pending = digit;
            }
            else
            {
                Set(_hours, _pending * 10 + digit);
                _pending = -1;
            }
            Invalidate();
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
                case Keys.Left:
                    SelectPart(HourPart);
                    break;
                case Keys.Right:
                    SelectPart(MinutePart);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.Handled) return;
            char c = e.KeyChar;
            if (c >= '0' && c <= '9')
            {
                Type(c - '0');
                e.Handled = true;
            }
            else if (c == ':' || c == '.')
            {
                SelectPart(MinutePart);
                e.Handled = true;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();

            int arrow = ArrowAt(e.Location);
            if (arrow != 0)
            {
                _pressedArrow = arrow;
                Step(arrow);
                _repeat.Interval = 400;
                _repeat.Start();
            }
            else
            {
                SelectPart(e.X < MinuteBounds.Left ? HourPart : MinutePart);
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            StopRepeat();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            StopRepeat();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hover = ArrowAt(e.Location);
            if (hover == _hoverArrow) return;
            _hoverArrow = hover;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverArrow = 0;
            Invalidate();
        }

        /// <summary>
        /// Das Rad dreht den Wert nur, wenn das Feld den Fokus hat und der Zeiger darueber steht; sonst reicht Windows es an
        /// die Seite weiter, die dann rollt.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Focused || e.Delta == 0 || !ClientRectangle.Contains(e.Location)) return;
            Step(e.Delta > 0 ? 1 : -1);
            var handled = e as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;
        }

        void StopRepeat()
        {
            _repeat.Stop();
            if (_pressedArrow == 0) return;
            _pressedArrow = 0;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _pending = -1;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _pending = -1;
            StopRepeat();
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        // ------------------------------------------------------------------ Groesse und Zeichnen

        void Fit()
        {
            int text = Metrics.TextWidth("00:00", Font);
            int height = _compact ? Math.Max(Dpi.Px(23), Font.Height + Dpi.Px(8)) : Math.Max(Dpi.Px(32), Font.Height + Dpi.Px(12));
            Size = new Size(Math.Max(Dpi.Px(84), text + Dpi.Px(48)), height);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Fit();
        }

        int DigitsWidth
        {
            get { return Metrics.TextWidth("00", Font); }
        }

        Rectangle HourBounds
        {
            get { return new Rectangle(Dpi.Px(10), 0, DigitsWidth, Height); }
        }

        Rectangle MinuteBounds
        {
            get
            {
                Rectangle hours = HourBounds;
                return new Rectangle(hours.Right + Metrics.TextWidth(":", Font), 0, DigitsWidth, Height);
            }
        }

        Rectangle ArrowBounds
        {
            get
            {
                int width = Dpi.Px(22);
                return new Rectangle(Width - width - Dpi.Px(1), Dpi.Px(1), width, Height - Dpi.Px(2));
            }
        }

        /// <summary>+1 ueber dem oberen Pfeil, -1 ueber dem unteren, sonst 0.</summary>
        int ArrowAt(Point point)
        {
            Rectangle arrows = ArrowBounds;
            if (!arrows.Contains(point)) return 0;
            return point.Y < arrows.Top + arrows.Height / 2 ? 1 : -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Background(g, this);

            bool enabled = Enabled;
            bool focused = Focused;
            Color border = enabled && focused ? Theme.Green : Theme.FieldBorder;
            Metrics.PaintRounded(g, ClientRectangle, Dpi.Px(6), enabled ? Theme.InputBack : Theme.Track, border);

            Rectangle hours = HourBounds;
            Rectangle minutes = MinuteBounds;
            DrawPart(g, hours, _hours, focused && _part == HourPart);
            Draw.CenteredText(g, ":", Font, new Rectangle(hours.Right, 0, minutes.Left - hours.Right, Height),
                enabled ? Theme.Text : Theme.Disabled);
            DrawPart(g, minutes, _minutes, focused && _part == MinutePart);

            Rectangle arrows = ArrowBounds;
            int middle = arrows.Top + arrows.Height / 2;
            int center = arrows.Left + arrows.Width / 2;
            DrawArrow(g, center, middle - Dpi.Px(4), true, ArrowColor(1));
            DrawArrow(g, center, middle + Dpi.Px(4), false, ArrowColor(-1));
        }

        void DrawPart(Graphics g, Rectangle bounds, int value, bool selected)
        {
            Color fore = Enabled ? Theme.Text : Theme.Disabled;
            if (selected)
            {
                int height = Font.Height + Dpi.Px(2);
                var mark = new Rectangle(bounds.X - Dpi.Px(2), (Height - height) / 2, bounds.Width + Dpi.Px(4), height);
                Metrics.PaintRounded(g, mark, Dpi.Px(3), Theme.GreenButton, Color.Empty);
                fore = Theme.OnAccent;
            }
            Draw.CenteredText(g, value.ToString("00", CultureInfo.InvariantCulture), Font, bounds, fore);
        }

        Color ArrowColor(int arrow)
        {
            if (!Enabled) return Theme.Disabled;
            if (_pressedArrow == arrow) return Theme.Green;
            return _hoverArrow == arrow ? Theme.Text : Theme.Muted;
        }

        static void DrawArrow(Graphics g, int x, int y, bool up, Color color)
        {
            float half = 3.5f * Dpi.Factor;
            float rise = 2f * Dpi.Factor;
            float tip = up ? y - rise : y + rise;
            float foot = up ? y + rise : y - rise;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, Math.Max(1.2f, 1.4f * Dpi.Factor)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new[] { new PointF(x - half, foot), new PointF(x, tip), new PointF(x + half, foot) });
            }
        }

        // ------------------------------------------------------------------ Screenreader

        /// <summary>Screenreader lesen den Wert als "08:00" und koennen ihn auch setzen.</summary>
        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new TimeBoxAccessibleObject(this);
        }

        sealed class TimeBoxAccessibleObject : ControlAccessibleObject
        {
            readonly TimeBox _owner;

            public TimeBoxAccessibleObject(TimeBox owner)
                : base(owner)
            {
                _owner = owner;
            }

            public override string Value
            {
                get { return ScheduleRule.FormatTime(_owner.Value); }
                set
                {
                    TimeSpan time;
                    if (ScheduleRule.TryParseTime(value, out time)) _owner.Value = time;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _repeat.Dispose();
            base.Dispose(disposing);
        }
    }
}
