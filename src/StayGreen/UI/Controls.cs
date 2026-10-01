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
        public static readonly Color Muted = Color.FromArgb(0x6B, 0x70, 0x78);

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

    /// <summary>Label, das bei fester Breite sauber umbricht und dabei die Hoehe korrekt meldet (auch im TableLayoutPanel).</summary>
    sealed class WrapLabel : Label
    {
        public WrapLabel()
        {
            AutoSize = true;
            UseMnemonic = false;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            int width = proposedSize.Width;
            if (width <= 1 || width > 10000)
            {
                width = Parent != null
                    ? Math.Max(60, Parent.ClientSize.Width - Parent.Padding.Horizontal - Margin.Horizontal)
                    : 300;
            }
            string text = string.IsNullOrEmpty(Text) ? " " : Text;
            Size measured = TextRenderer.MeasureText(text, Font, new Size(width, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl);
            return new Size(width, measured.Height + Padding.Vertical);
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
