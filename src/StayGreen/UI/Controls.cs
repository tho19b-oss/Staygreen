using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using StayGreen.Core;
using StayGreen.Platform;

namespace StayGreen.UI
{
    enum ThemeMode
    {
        Light,
        Dark,

        /// <summary>Windows-Kontrastdesign: Alle Farben kommen aus den Systemfarben.</summary>
        HighContrast,
    }

    /// <summary>
    /// Die Farbpalette. Sie wird einmal beim Start (und beim Wechsel der Darstellung) mit <see cref="Apply"/> gewaehlt;
    /// die Oberflaeche baut sich danach neu auf, weil viele Bedienelemente ihre Farben beim Anlegen uebernehmen.
    /// </summary>
    static class Theme
    {
        public static ThemeMode Mode { get; private set; }

        // Statusfarben
        public static Color Green, GreenDark, Gray, GrayDark, Amber, Red;

        // Flaechen und Text
        public static Color Background, Card, CardBorder, Divider, FieldBorder, Track, Chip;
        public static Color Text, Muted, MutedStrong, Disabled;

        /// <summary>Schrift und Symbole auf farbig gefuellten Flaechen (Knoepfe, Schalter, Statuslampe).</summary>
        public static Color OnAccent;

        // Knoepfe (dunkler als die Statusfarbe, damit weisse Schrift gut lesbar bleibt)
        public static Color GreenButton, GreenButtonHover, GreenButtonDown;
        public static Color GrayButtonHover, GrayButtonDown;

        // Schalter
        public static Color SwitchOffFill, SwitchOffBorder, SwitchOffThumb, SwitchOnDisabled;

        // Eingabefelder (nur im dunklen Design selbst gefaerbt)
        public static Color InputBack;

        // Zarte Hintergruende der Statuskarte
        static Color _tintActive, _tintActiveBorder, _tintWait, _tintWaitBorder;
        static Color _tintBad, _tintBadBorder, _tintStopped, _tintStoppedBorder;

        static Theme()
        {
            ApplyLight();
        }

        public static bool IsDark
        {
            get { return Mode == ThemeMode.Dark; }
        }

        /// <summary>
        /// Welche Darstellung die Einstellung ergibt: "auto" folgt Windows (Hell/Dunkel), "light" und "dark" sind fest.
        /// Ein Kontrastdesign von Windows gewinnt immer.
        /// </summary>
        public static ThemeMode Resolve(string setting)
        {
            if (SafeHighContrast()) return ThemeMode.HighContrast;
            if (setting == "dark" || (setting != "light" && WindowsPrefersDark())) return ThemeMode.Dark;
            return ThemeMode.Light;
        }

        public static void Apply(string setting)
        {
            switch (Resolve(setting))
            {
                case ThemeMode.HighContrast: ApplyHighContrast(); break;
                case ThemeMode.Dark: ApplyDark(); break;
                default: ApplyLight(); break;
            }
        }

        static bool SafeHighContrast()
        {
            try { return SystemInformation.HighContrast; }
            catch { return false; }
        }

        /// <summary>Windows 10/11: "Apps im dunklen Modus" (0 = dunkel).</summary>
        static bool WindowsPrefersDark()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        static void ApplyLight()
        {
            Mode = ThemeMode.Light;
            Green = Color.FromArgb(0x2E, 0xA0, 0x5A);
            GreenDark = Color.FromArgb(0x27, 0x8A, 0x4D);
            Gray = Color.FromArgb(0x8A, 0x8F, 0x98);
            GrayDark = Color.FromArgb(0x4A, 0x4F, 0x57);
            Amber = Color.FromArgb(0xD9, 0x9A, 0x00);
            Red = Color.FromArgb(0xD6, 0x45, 0x45);

            Background = Color.FromArgb(0xF3, 0xF5, 0xF7);
            Card = Color.White;
            CardBorder = Color.FromArgb(0xE1, 0xE5, 0xEA);
            Divider = Color.FromArgb(0xEC, 0xEF, 0xF2);
            FieldBorder = Color.FromArgb(0xC4, 0xCA, 0xD2);
            Track = Color.FromArgb(0xE3, 0xE7, 0xEB);
            Chip = Color.FromArgb(0xEE, 0xF1, 0xF4);
            Text = Color.FromArgb(0x1F, 0x23, 0x28);
            Muted = Color.FromArgb(0x6B, 0x70, 0x78);
            MutedStrong = Color.FromArgb(0x56, 0x5B, 0x63);
            Disabled = Color.FromArgb(0xA3, 0xA9, 0xB1);
            OnAccent = Color.White;
            InputBack = Color.White;

            GreenButton = Color.FromArgb(0x1F, 0x7F, 0x47);
            GreenButtonHover = Color.FromArgb(0x19, 0x6B, 0x3B);
            GreenButtonDown = Color.FromArgb(0x14, 0x5A, 0x31);
            GrayButtonHover = Color.FromArgb(0x35, 0x39, 0x40);
            GrayButtonDown = Color.FromArgb(0x2A, 0x2D, 0x33);

            SwitchOffFill = Color.FromArgb(0xE9, 0xEC, 0xEF);
            SwitchOffBorder = Color.FromArgb(0x8A, 0x90, 0x99);
            SwitchOffThumb = Color.FromArgb(0x5F, 0x65, 0x6D);
            SwitchOnDisabled = Color.FromArgb(0xA9, 0xD8, 0xBC);

            _tintActive = Color.FromArgb(0xE6, 0xF4, 0xEA);
            _tintActiveBorder = Color.FromArgb(0xBF, 0xE3, 0xCA);
            _tintWait = Color.FromArgb(0xFF, 0xF4, 0xD6);
            _tintWaitBorder = Color.FromArgb(0xF1, 0xDB, 0x9E);
            _tintBad = Color.FromArgb(0xFD, 0xE8, 0xE8);
            _tintBadBorder = Color.FromArgb(0xF0, 0xBF, 0xBF);
            _tintStopped = Color.FromArgb(0xEC, 0xEE, 0xF1);
            _tintStoppedBorder = Color.FromArgb(0xD9, 0xDD, 0xE2);
        }

        static void ApplyDark()
        {
            Mode = ThemeMode.Dark;
            Green = Color.FromArgb(0x34, 0xB1, 0x68);
            GreenDark = Color.FromArgb(0x2B, 0x9A, 0x58);
            Gray = Color.FromArgb(0x7C, 0x83, 0x8C);
            GrayDark = Color.FromArgb(0x5A, 0x61, 0x6A);
            Amber = Color.FromArgb(0xE0, 0xA8, 0x1A);
            Red = Color.FromArgb(0xE5, 0x59, 0x59);

            Background = Color.FromArgb(0x17, 0x19, 0x1C);
            Card = Color.FromArgb(0x22, 0x26, 0x2B);
            CardBorder = Color.FromArgb(0x34, 0x3A, 0x41);
            Divider = Color.FromArgb(0x2E, 0x33, 0x39);
            FieldBorder = Color.FromArgb(0x55, 0x5D, 0x66);
            Track = Color.FromArgb(0x2D, 0x32, 0x38);
            Chip = Color.FromArgb(0x2C, 0x31, 0x37);
            Text = Color.FromArgb(0xE9, 0xEC, 0xEF);
            Muted = Color.FromArgb(0xA3, 0xAA, 0xB2);
            MutedStrong = Color.FromArgb(0xBC, 0xC2, 0xC9);
            Disabled = Color.FromArgb(0x6B, 0x72, 0x7A);
            OnAccent = Color.White;
            InputBack = Color.FromArgb(0x2B, 0x30, 0x36);

            GreenButton = Color.FromArgb(0x23, 0x86, 0x4F);
            GreenButtonHover = Color.FromArgb(0x29, 0x96, 0x58);
            GreenButtonDown = Color.FromArgb(0x1D, 0x72, 0x43);
            GrayButtonHover = Color.FromArgb(0x6A, 0x72, 0x7C);
            GrayButtonDown = Color.FromArgb(0x4D, 0x54, 0x5C);

            SwitchOffFill = Color.FromArgb(0x33, 0x38, 0x3E);
            SwitchOffBorder = Color.FromArgb(0x80, 0x87, 0x90);
            SwitchOffThumb = Color.FromArgb(0xB6, 0xBC, 0xC4);
            SwitchOnDisabled = Color.FromArgb(0x3A, 0x62, 0x4C);

            _tintActive = Color.FromArgb(0x1C, 0x33, 0x27);
            _tintActiveBorder = Color.FromArgb(0x2F, 0x5C, 0x43);
            _tintWait = Color.FromArgb(0x3A, 0x31, 0x17);
            _tintWaitBorder = Color.FromArgb(0x6B, 0x58, 0x25);
            _tintBad = Color.FromArgb(0x3C, 0x22, 0x23);
            _tintBadBorder = Color.FromArgb(0x70, 0x3B, 0x3B);
            _tintStopped = Color.FromArgb(0x27, 0x2B, 0x30);
            _tintStoppedBorder = Color.FromArgb(0x3B, 0x41, 0x48);
        }

        static void ApplyHighContrast()
        {
            Mode = ThemeMode.HighContrast;
            Green = SystemColors.Highlight;
            GreenDark = SystemColors.Highlight;
            Gray = SystemColors.GrayText;
            GrayDark = SystemColors.ControlText;
            Amber = SystemColors.HotTrack;
            Red = SystemColors.HotTrack;

            Background = SystemColors.Window;
            Card = SystemColors.Window;
            CardBorder = SystemColors.WindowText;
            Divider = SystemColors.GrayText;
            FieldBorder = SystemColors.WindowText;
            Track = SystemColors.Control;
            Chip = SystemColors.Control;
            Text = SystemColors.WindowText;
            Muted = SystemColors.WindowText;
            MutedStrong = SystemColors.WindowText;
            Disabled = SystemColors.GrayText;
            OnAccent = SystemColors.HighlightText;
            InputBack = SystemColors.Window;

            GreenButton = SystemColors.Highlight;
            GreenButtonHover = SystemColors.HotTrack;
            GreenButtonDown = SystemColors.HotTrack;
            GrayButtonHover = SystemColors.HotTrack;
            GrayButtonDown = SystemColors.HotTrack;

            SwitchOffFill = SystemColors.Window;
            SwitchOffBorder = SystemColors.WindowText;
            SwitchOffThumb = SystemColors.WindowText;
            SwitchOnDisabled = SystemColors.GrayText;

            _tintActive = _tintWait = _tintBad = _tintStopped = SystemColors.Window;
            _tintActiveBorder = _tintWaitBorder = _tintBadBorder = _tintStoppedBorder = SystemColors.WindowText;
        }

        public static Color ColorFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Active:
                case StatusKind.StandingBy:
                    return Green;
                case StatusKind.WaitingForWindow:
                case StatusKind.Paused:
                case StatusKind.WaitingForTeams:
                    return Amber;
                case StatusKind.Blocked:
                case StatusKind.SessionLocked:
                    return Red;
                default:
                    return Gray;
            }
        }

        /// <summary>Zarter Hintergrund der Statuskarte passend zum Zustand.</summary>
        public static Color TintFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Active:
                case StatusKind.StandingBy:
                    return _tintActive;
                case StatusKind.WaitingForWindow:
                case StatusKind.Paused:
                case StatusKind.WaitingForTeams:
                    return _tintWait;
                case StatusKind.Blocked:
                case StatusKind.SessionLocked:
                    return _tintBad;
                default:
                    return _tintStopped;
            }
        }

        public static Color TintBorderFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Active:
                case StatusKind.StandingBy:
                    return _tintActiveBorder;
                case StatusKind.WaitingForWindow:
                case StatusKind.Paused:
                case StatusKind.WaitingForTeams:
                    return _tintWaitBorder;
                case StatusKind.Blocked:
                case StatusKind.SessionLocked:
                    return _tintBadBorder;
                default:
                    return _tintStoppedBorder;
            }
        }

        public static Glyph GlyphFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Active:
                case StatusKind.StandingBy:
                    return Glyph.Check;
                case StatusKind.WaitingForWindow:
                case StatusKind.Paused:
                case StatusKind.WaitingForTeams:
                    return Glyph.Pause;
                case StatusKind.Blocked:
                case StatusKind.SessionLocked:
                    return Glyph.Exclaim;
                default:
                    return Glyph.Dash;
            }
        }

        /// <summary>
        /// Faerbt ein Standard-Eingabefeld (Zahl, Auswahl, Text, Datum) passend zum dunklen Design. Hell und Kontrastdesign
        /// uebernimmt Windows selbst.
        /// </summary>
        public static void StyleInput(Control control)
        {
            if (Mode != ThemeMode.Dark || control == null) return;

            control.BackColor = InputBack;
            control.ForeColor = Text;

            var combo = control as ComboBox;
            if (combo != null) combo.FlatStyle = FlatStyle.Flat;

            var picker = control as DateTimePicker;
            if (picker != null)
            {
                picker.CalendarMonthBackground = Card;
                picker.CalendarForeColor = Text;
                picker.CalendarTitleBackColor = GreenButton;
                picker.CalendarTitleForeColor = OnAccent;
                picker.CalendarTrailingForeColor = Disabled;
            }
        }
    }

    /// <summary>Kleines Symbol im farbigen Kreis (zusaetzlich zur Farbe, damit der Zustand nicht nur ueber Farbe lesbar ist).</summary>
    enum Glyph
    {
        Check,
        Pause,
        Exclaim,
        Dash,
    }

    /// <summary>Zeichnet Kreis + Symbol; gemeinsam genutzt von Statuslampe, Tray-Icon und Fenster-Icon.</summary>
    static class GlyphPainter
    {
        public static void Draw(Graphics g, float size, Color color, Glyph glyph)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float pad = size * 0.04f;
            var circle = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
            using (var brush = new SolidBrush(color))
                g.FillEllipse(brush, circle);
            using (var ring = new Pen(Color.FromArgb(70, 0, 0, 0), Math.Max(1f, size * 0.035f)))
                g.DrawEllipse(ring, circle);

            float w = Math.Max(1.6f, size * 0.11f);
            using (var pen = new Pen(Theme.OnAccent, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var fill = new SolidBrush(Theme.OnAccent))
            {
                switch (glyph)
                {
                    case Glyph.Check:
                        g.DrawLines(pen, new[]
                        {
                            new PointF(size * 0.27f, size * 0.53f),
                            new PointF(size * 0.43f, size * 0.69f),
                            new PointF(size * 0.74f, size * 0.34f),
                        });
                        break;
                    case Glyph.Pause:
                        g.DrawLine(pen, size * 0.38f, size * 0.30f, size * 0.38f, size * 0.70f);
                        g.DrawLine(pen, size * 0.62f, size * 0.30f, size * 0.62f, size * 0.70f);
                        break;
                    case Glyph.Exclaim:
                        g.DrawLine(pen, size * 0.5f, size * 0.26f, size * 0.5f, size * 0.55f);
                        float dot = Math.Max(2f, size * 0.12f);
                        g.FillEllipse(fill, size * 0.5f - dot / 2, size * 0.68f - dot / 2, dot, dot);
                        break;
                    default:
                        g.DrawLine(pen, size * 0.32f, size * 0.5f, size * 0.68f, size * 0.5f);
                        break;
                }
            }
        }
    }

    sealed class StatusLamp : Control
    {
        Color _color = Theme.Gray;
        Glyph _glyph = Glyph.Dash;

        public StatusLamp()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(48, 48);
            TabStop = false;
        }

        public void Set(Color color, Glyph glyph)
        {
            if (_color == color && _glyph == glyph) return;
            _color = color;
            _glyph = glyph;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float size = Math.Min(Width, Height);
            GlyphPainter.Draw(e.Graphics, size, _color, _glyph);
        }
    }

    /// <summary>Label, das bei vorgegebener Breite umbricht und die dafuer noetige Hoehe selbst berechnet.</summary>
    sealed class WrapLabel : Label, IMeasurable
    {
        public WrapLabel()
        {
            AutoSize = false;
            UseMnemonic = false;
        }

        public int MeasureHeight(int width)
        {
            return Metrics.TextHeight(Text, Font, width - Padding.Horizontal) + Padding.Vertical + Dpi.Px(2);
        }
    }

    /// <summary>Gemeinsame Hilfen fuer die selbst gezeichneten Bedienelemente.</summary>
    static class Draw
    {
        const TextFormatFlags Centered = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        public static void Background(Graphics g, Control c)
        {
            g.Clear(c.Parent != null ? c.Parent.BackColor : Theme.Background);
        }

        public static void CenteredText(Graphics g, string text, Font font, Rectangle bounds, Color color)
        {
            TextRenderer.DrawText(g, text, font, bounds, color, Centered);
        }

        /// <summary>Gepunkteter Rahmen fuer die Tastaturbedienung.</summary>
        public static void FocusRing(Graphics g, Rectangle bounds, int radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (GraphicsPath path = Metrics.RoundRect(r, radius))
            using (var pen = new Pen(Theme.Text) { DashStyle = DashStyle.Dot })
                g.DrawPath(pen, path);
        }

        public static void Ellipse(Graphics g, Rectangle bounds, Color color)
        {
            using (var brush = new SolidBrush(color)) g.FillEllipse(brush, bounds);
        }
    }

    /// <summary>Ein/Aus-Schalter (statt Haekchen). Ist ein normales CheckBox-Element, kann also wie eines benutzt werden.</summary>
    sealed class SwitchBox : CheckBox
    {
        public SwitchBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Size = new Size(Dpi.Px(42), Dpi.Px(24));
            Cursor = Cursors.Hand;
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Background(g, this);

            bool on = Checked;
            bool enabled = Enabled;
            var track = new Rectangle(Dpi.Px(1), Dpi.Px(1), Width - Dpi.Px(2), Height - Dpi.Px(2));
            Color fill = on ? (enabled ? Theme.Green : Theme.SwitchOnDisabled) : Theme.SwitchOffFill;
            Color border = on ? fill : (enabled ? Theme.SwitchOffBorder : Theme.FieldBorder);
            Metrics.PaintRounded(g, track, track.Height / 2, fill, border);

            int margin = Dpi.Px(on ? 4 : 5);
            int thumb = track.Height - 2 * margin;
            int x = on ? track.Right - margin - thumb : track.Left + margin;
            Color thumbColor = on ? Theme.OnAccent : (enabled ? Theme.SwitchOffThumb : Theme.Disabled);
            Draw.Ellipse(g, new Rectangle(x, track.Top + margin, thumb, thumb), thumbColor);

            if (Focused && ShowFocusCues) Draw.FocusRing(g, ClientRectangle, ClientRectangle.Height / 2);
        }
    }

    /// <summary>Kleine runde Schaltflaeche zum Ein-/Ausschalten (Wochentage, Umschalttasten).</summary>
    sealed class ChipBox : CheckBox
    {
        public ChipBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
        }

        void Fit()
        {
            Size = new Size(Metrics.TextWidth(Text, Font) + Dpi.Px(26), Math.Max(Dpi.Px(32), Font.Height + Dpi.Px(12)));
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Fit();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Fit();
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Background(g, this);

            bool on = Checked;
            bool enabled = Enabled;
            Color fill = on ? (enabled ? Theme.GreenButton : Theme.SwitchOnDisabled) : Theme.Chip;
            Color border = on ? fill : Theme.CardBorder;
            Color fore = on ? Theme.OnAccent : (enabled ? Theme.Text : Theme.Disabled);
            Metrics.PaintRounded(g, ClientRectangle, Height / 2, fill, border);
            Draw.CenteredText(g, Text, Font, ClientRectangle, fore);
            if (Focused && ShowFocusCues) Draw.FocusRing(g, ClientRectangle, Height / 2);
        }
    }

    enum ButtonKind
    {
        /// <summary>Hauptaktion: gruen gefuellt.</summary>
        Primary,

        /// <summary>Nebenaktion: weiss mit Rand.</summary>
        Secondary,

        /// <summary>Dunkel gefuellt (z. B. "Stoppen").</summary>
        Dark,
    }

    /// <summary>Abgerundeter Knopf. Passt seine Groesse selbst dem Text an.</summary>
    sealed class FlatButton : Button
    {
        bool _hover;
        bool _down;
        ButtonKind _kind = ButtonKind.Secondary;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }

        /// <summary>Mindestbreite in logischen Pixeln.</summary>
        public int MinWidth { get; set; }

        public ButtonKind Kind
        {
            get { return _kind; }
            set
            {
                if (_kind == value) return;
                _kind = value;
                Invalidate();
            }
        }

        void Fit()
        {
            Size = new Size(Math.Max(Dpi.Px(MinWidth), Metrics.TextWidth(Text, Font) + Dpi.Px(32)),
                Math.Max(Dpi.Px(36), Font.Height + Dpi.Px(16)));
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Fit();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Fit();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _down = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _down = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _down = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Background(g, this);

            Color fill, fore, border = Color.Empty;
            if (!Enabled)
            {
                fill = Theme.Track;
                fore = Theme.Disabled;
            }
            else if (_kind == ButtonKind.Primary)
            {
                fill = _down ? Theme.GreenButtonDown : (_hover ? Theme.GreenButtonHover : Theme.GreenButton);
                fore = Theme.OnAccent;
            }
            else if (_kind == ButtonKind.Dark)
            {
                fill = _down ? Theme.GrayButtonDown : (_hover ? Theme.GrayButtonHover : Theme.GrayDark);
                fore = Theme.OnAccent;
            }
            else
            {
                fill = _down ? Theme.Track : (_hover ? Theme.Chip : Theme.Card);
                fore = Theme.Text;
                border = Theme.FieldBorder;
            }

            Metrics.PaintRounded(g, ClientRectangle, Dpi.Px(8), fill, border);
            Draw.CenteredText(g, Text, Font, ClientRectangle, fore);
            if (Focused && ShowFocusCues)
                Draw.FocusRing(g, Rectangle.Inflate(ClientRectangle, -Dpi.Px(3), -Dpi.Px(3)), Dpi.Px(6));
        }
    }

    /// <summary>Eine Auswahl aus wenigen Moeglichkeiten als Leiste (Registerkarten, "Taeglich/Einmalig").</summary>
    sealed class Segmented : Control
    {
        string[] _items = Array.Empty<string>();
        int _selected;
        int _hover = -1;

        public Segmented()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.PageTabList;
        }

        public event EventHandler SelectedIndexChanged;

        /// <summary>Groesse automatisch den Eintraegen anpassen (fuer Leisten, die nicht die ganze Breite fuellen).</summary>
        public bool AutoFit { get; set; }

        public string[] Items
        {
            get { return _items; }
            set
            {
                _items = value ?? Array.Empty<string>();
                if (_selected >= _items.Length) _selected = Math.Max(0, _items.Length - 1);
                if (AutoFit) Fit();
                Invalidate();
            }
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                if (_items.Length == 0) return;
                value = Math.Max(0, Math.Min(_items.Length - 1, value));
                if (value == _selected) return;
                _selected = value;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.Selection, _selected + 1);
                EventHandler handler = SelectedIndexChanged;
                if (handler != null) handler(this, EventArgs.Empty);
            }
        }

        public int PreferredHeight
        {
            get { return Math.Max(Dpi.Px(38), Font.Height + Dpi.Px(20)); }
        }

        /// <summary>Breite, bei der alle Eintraege bequem Platz haben.</summary>
        public int PreferredWidth
        {
            get
            {
                int widest = 0;
                foreach (string item in _items) widest = Math.Max(widest, Metrics.TextWidth(item, Font));
                return Math.Max(1, _items.Length) * (widest + Dpi.Px(34)) + 2 * Dpi.Px(3);
            }
        }

        public void Fit()
        {
            Size = new Size(PreferredWidth, PreferredHeight);
        }

        /// <summary>Fuer Screenreader: jeder Eintrag der Leiste ist ein Reiter mit Namen und Auswahlzustand.</summary>
        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new SegmentedAccessibleObject(this);
        }

        sealed class SegmentedAccessibleObject : ControlAccessibleObject
        {
            readonly Segmented _owner;

            public SegmentedAccessibleObject(Segmented owner)
                : base(owner)
            {
                _owner = owner;
            }

            public override int GetChildCount()
            {
                return _owner._items.Length;
            }

            public override AccessibleObject GetChild(int index)
            {
                return index >= 0 && index < _owner._items.Length ? new SegmentItemAccessibleObject(_owner, index) : null;
            }

            public override AccessibleObject GetSelected()
            {
                return GetChild(_owner._selected);
            }

            public override AccessibleObject GetFocused()
            {
                return _owner.Focused ? GetChild(_owner._selected) : null;
            }
        }

        sealed class SegmentItemAccessibleObject : AccessibleObject
        {
            readonly Segmented _owner;
            readonly int _index;

            public SegmentItemAccessibleObject(Segmented owner, int index)
            {
                _owner = owner;
                _index = index;
            }

            public override string Name
            {
                get { return _owner._items[_index]; }
                set { }
            }

            public override AccessibleRole Role
            {
                get { return AccessibleRole.PageTab; }
            }

            public override AccessibleObject Parent
            {
                get { return _owner.AccessibilityObject; }
            }

            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates state = AccessibleStates.Selectable | AccessibleStates.Focusable;
                    if (_index == _owner._selected)
                    {
                        state |= AccessibleStates.Selected;
                        if (_owner.Focused) state |= AccessibleStates.Focused;
                    }
                    return state;
                }
            }

            public override Rectangle Bounds
            {
                get { return _owner.RectangleToScreen(_owner.SegmentRect(_index)); }
            }

            public override string DefaultAction
            {
                get { return "Select"; }
            }

            public override void DoDefaultAction()
            {
                _owner.SelectedIndex = _index;
            }

            public override void Select(AccessibleSelection flags)
            {
                if ((flags & AccessibleSelection.TakeSelection) != 0) _owner.SelectedIndex = _index;
            }
        }

        Rectangle SegmentRect(int index)
        {
            int inset = Dpi.Px(3);
            int count = Math.Max(1, _items.Length);
            int inner = Width - 2 * inset;
            int x = inset + inner * index / count;
            int right = inset + inner * (index + 1) / count;
            return new Rectangle(x, inset, right - x, Height - 2 * inset);
        }

        int HitTest(int x)
        {
            for (int i = 0; i < _items.Length; i++)
            {
                Rectangle r = SegmentRect(i);
                if (x >= r.Left && x < r.Right) return i;
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hit = HitTest(e.X);
            if (hit != _hover)
            {
                _hover = hit;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            int hit = HitTest(e.X);
            if (hit >= 0) SelectedIndex = hit;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left) { SelectedIndex = _selected - 1; e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { SelectedIndex = _selected + 1; e.Handled = true; }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (AutoFit) Fit();
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Background(g, this);
            Metrics.PaintRounded(g, ClientRectangle, Dpi.Px(10), Theme.Track, Color.Empty);

            for (int i = 0; i < _items.Length; i++)
            {
                Rectangle r = SegmentRect(i);
                bool selected = i == _selected;
                if (selected)
                    Metrics.PaintRounded(g, r, Dpi.Px(8), Theme.Card, Theme.CardBorder);
                Color fore = selected ? Theme.Text : (i == _hover ? Theme.Text : Theme.MutedStrong);
                Draw.CenteredText(g, _items[i], Font, r, fore);
                if (selected && Focused && ShowFocusCues)
                    Draw.FocusRing(g, Rectangle.Inflate(r, -Dpi.Px(2), -Dpi.Px(2)), Dpi.Px(6));
            }
        }
    }

    static class IconFactory
    {
        /// <summary>Zeichnet ein Icon zur Laufzeit; der GDI-Handle wird sauber freigegeben (kein Leck bei jedem Wechsel).</summary>
        public static Icon Create(Color color, Glyph glyph, int size)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    GlyphPainter.Draw(g, size, color, glyph);
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(handle))
                        return (Icon)temp.Clone();
                }
                finally
                {
                    if (PlatformInfo.IsWindows) NativeMethods.DestroyIcon(handle);
                }
            }
        }
    }
}
