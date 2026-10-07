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
    /// Pfeile rechts aendern den gewaehlten Teil, Pfeil links/rechts (oder ":") wechselt ihn. Rahmen, Pfeile und Mausrad
    /// kommen von <see cref="SpinField"/>.
    /// </summary>
    sealed class TimeBox : SpinField
    {
        const int HourPart = 0;
        const int MinutePart = 1;

        int _hours;
        int _minutes;
        int _part = HourPart;
        int _pending = -1;     // erste Ziffer einer zweistelligen Eingabe, -1 = keine

        public TimeBox()
        {
            Fit();
        }

        public event EventHandler ValueChanged;

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
        protected override void Step(int delta)
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

        /// <summary>Ein Klick waehlt den Teil, auf den er trifft.</summary>
        protected override void OnContentClick(Point location)
        {
            Rectangle hours, minutes;
            GetParts(out hours, out minutes);
            SelectPart(location.X < (hours.Right + minutes.Left) / 2 ? HourPart : MinutePart);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _pending = -1;
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _pending = -1;
        }

        // ------------------------------------------------------------------ Groesse und Zeichnen

        protected override int ContentWidth
        {
            get { return Metrics.TextWidth("00:00", Font); }
        }

        /// <summary>
        /// Wo im Text "HH:mm" Stunden und Minuten stehen (fuer Markierung und Mausklick). Gemessen werden nur Ziffernfolgen
        /// und der ganze Text: Den Doppelpunkt allein misst Windows breiter, als er im Text steht.
        /// </summary>
        void GetParts(out Rectangle hours, out Rectangle minutes)
        {
            int digits = Metrics.TextWidth("0000", Font);
            int pair = digits / 2;
            int colon = Math.Max(1, Metrics.TextWidth("00:00", Font) - digits);
            int left = TextLeft;
            hours = new Rectangle(left, 0, pair, Height);
            minutes = new Rectangle(left + pair + colon, 0, pair, Height);
        }

        protected override void PaintContent(Graphics g, Color fore)
        {
            // Die Uhrzeit in einem Zug zeichnen, damit Ziffern und Doppelpunkt so dicht stehen wie in normalem Text.
            Rectangle hours, minutes;
            GetParts(out hours, out minutes);
            string text = _hours.ToString("00", CultureInfo.InvariantCulture) + ":"
                + _minutes.ToString("00", CultureInfo.InvariantCulture);
            var bounds = new Rectangle(hours.Left, 0, minutes.Right - hours.Left + Dpi.Px(6), Height);
            if (!Focused)
            {
                TextRenderer.DrawText(g, text, Font, bounds, fore, TextFlags);
                return;
            }

            // Der gewaehlte Teil gruen hinterlegt und weiss: derselbe Text zweimal, jeweils passend beschnitten. Zum
            // Doppelpunkt hin bleibt die Markierung schmal, damit sie ihn nicht anschneidet.
            Rectangle part = _part == HourPart ? hours : minutes;
            int outer = Dpi.Px(2);
            int inner = Dpi.Px(1);
            int height = Font.Height + Dpi.Px(2);
            int markLeft = part.X - (_part == HourPart ? outer : inner);
            var mark = new Rectangle(markLeft, (Height - height) / 2, part.Width + outer + inner, height);
            Metrics.PaintRounded(g, mark, Dpi.Px(3), Theme.GreenButton, Color.Empty);
            using (var outside = new Region(ClientRectangle))
            {
                outside.Exclude(mark);
                g.SetClip(outside, CombineMode.Replace);
                TextRenderer.DrawText(g, text, Font, bounds, fore, TextFlags);
            }
            g.SetClip(mark);
            TextRenderer.DrawText(g, text, Font, bounds, Theme.OnAccent, TextFlags);
            g.ResetClip();
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
    }
}
