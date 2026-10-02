using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using StayGreen.Core;
using StayGreen.Platform;

namespace StayGreen.UI
{
    static class Theme
    {
        public static readonly Color Green = Color.FromArgb(0x2E, 0xA0, 0x5A);
        public static readonly Color GreenDark = Color.FromArgb(0x27, 0x8A, 0x4D);
        public static readonly Color Gray = Color.FromArgb(0x8A, 0x8F, 0x98);
        public static readonly Color GrayDark = Color.FromArgb(0x4A, 0x4F, 0x57);
        public static readonly Color Amber = Color.FromArgb(0xD9, 0x9A, 0x00);
        public static readonly Color Red = Color.FromArgb(0xD6, 0x45, 0x45);

        // Flaechen und Text
        public static readonly Color Background = Color.FromArgb(0xF3, 0xF5, 0xF7);
        public static readonly Color Card = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(0xE1, 0xE5, 0xEA);
        public static readonly Color Divider = Color.FromArgb(0xEC, 0xEF, 0xF2);
        public static readonly Color FieldBorder = Color.FromArgb(0xC4, 0xCA, 0xD2);
        public static readonly Color Track = Color.FromArgb(0xE3, 0xE7, 0xEB);
        public static readonly Color Chip = Color.FromArgb(0xEE, 0xF1, 0xF4);
        public static readonly Color Text = Color.FromArgb(0x1F, 0x23, 0x28);
        public static readonly Color Muted = Color.FromArgb(0x6B, 0x70, 0x78);
        public static readonly Color MutedStrong = Color.FromArgb(0x56, 0x5B, 0x63);
        public static readonly Color Disabled = Color.FromArgb(0xA3, 0xA9, 0xB1);

        // Knoepfe (dunkler als die Statusfarbe, damit weisse Schrift gut lesbar bleibt)
        public static readonly Color GreenButton = Color.FromArgb(0x1F, 0x7F, 0x47);
        public static readonly Color GreenButtonHover = Color.FromArgb(0x19, 0x6B, 0x3B);
        public static readonly Color GreenButtonDown = Color.FromArgb(0x14, 0x5A, 0x31);
        public static readonly Color GrayButtonHover = Color.FromArgb(0x35, 0x39, 0x40);
        public static readonly Color GrayButtonDown = Color.FromArgb(0x2A, 0x2D, 0x33);

        // Schalter
        public static readonly Color SwitchOffFill = Color.FromArgb(0xE9, 0xEC, 0xEF);
        public static readonly Color SwitchOffBorder = Color.FromArgb(0x8A, 0x90, 0x99);
        public static readonly Color SwitchOffThumb = Color.FromArgb(0x5F, 0x65, 0x6D);
        public static readonly Color SwitchOnDisabled = Color.FromArgb(0xA9, 0xD8, 0xBC);

        public static Color ColorFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Active:
                case StatusKind.StandingBy:
                    return Green;
                case StatusKind.WaitingForWindow:
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
                    return Color.FromArgb(0xE6, 0xF4, 0xEA);
                case StatusKind.WaitingForWindow:
                    return Color.FromArgb(0xFF, 0xF4, 0xD6);
                case StatusKind.Blocked:
                case StatusKind.SessionLocked:
                    return Color.FromArgb(0xFD, 0xE8, 0xE8);
                default:
                    return Color.FromArgb(0xEC, 0xEE, 0xF1);
            }
        }

        public static Color TintBorderFor(StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Active:
                case StatusKind.StandingBy:
                    return Color.FromArgb(0xBF, 0xE3, 0xCA);
                case StatusKind.WaitingForWindow:
                    return Color.FromArgb(0xF1, 0xDB, 0x9E);
                case StatusKind.Blocked:
                case StatusKind.SessionLocked:
                    return Color.FromArgb(0xF0, 0xBF, 0xBF);
                default:
                    return Color.FromArgb(0xD9, 0xDD, 0xE2);
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
                    return Glyph.Pause;
                case StatusKind.Blocked:
                case StatusKind.SessionLocked:
                    return Glyph.Exclaim;
                default:
                    return Glyph.Dash;
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
            using (var pen = new Pen(Color.White, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var fill = new SolidBrush(Color.White))
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
            Color thumbColor = on ? Color.White : (enabled ? Theme.SwitchOffThumb : Theme.Disabled);
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
            Color fore = on ? Color.White : (enabled ? Theme.Text : Theme.Disabled);
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
                fore = Color.White;
            }
            else if (_kind == ButtonKind.Dark)
            {
                fill = _down ? Theme.GrayButtonDown : (_hover ? Theme.GrayButtonHover : Theme.GrayDark);
                fore = Color.White;
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
        string[] _items = new string[0];
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
                _items = value ?? new string[0];
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
