using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StayGreen.UI
{
    /// <summary>
    /// Gemeinsame Basis der selbst gezeichneten Eingabefelder mit zwei kleinen Pfeilen rechts (<see cref="TimeBox"/>,
    /// <see cref="NumberBox"/>, <see cref="DateBox"/>): ein abgerundetes Feld im Stil der Karten, das sich anders als die
    /// Windows-Felder auch im dunklen Design einfaerbt. Mit dem Fokus ist der Rand gruen. Ein Pfeil aendert den Wert um einen Schritt, gedrueckt
    /// gehalten zaehlt er nach einer kurzen Pause schnell weiter (wie beim Windows-Feld); geht es in eine Richtung nicht
    /// weiter, ist der Pfeil ausgegraut. Das Mausrad aendert den Wert nur, solange das Feld den Fokus hat; sonst rollt wie
    /// gewohnt die Seite.
    /// </summary>
    abstract class SpinField : Control
    {
        /// <summary>Text links beginnend und senkrecht zentriert, ohne Innenabstand (so lassen sich Teile genau vermessen).</summary>
        protected const TextFormatFlags TextFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
            | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping;

        readonly Timer _repeat = new Timer();
        int _hoverArrow;       // +1 = oberer Pfeil, -1 = unterer, 0 = keiner
        int _pressedArrow;

        protected SpinField()
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
        }

        /// <summary>Linker Rand des Inhalts.</summary>
        protected static int TextLeft
        {
            get { return Dpi.Px(10); }
        }

        /// <summary>Platz, den der breiteste Inhalt braucht; danach richtet sich die Breite des Felds.</summary>
        protected abstract int ContentWidth { get; }

        /// <summary>Den Wert um einen Schritt aendern: +1 nach oben, -1 nach unten.</summary>
        protected abstract void Step(int delta);

        /// <summary>Ob ein Schritt in diese Richtung den Wert aendert; sonst ist der Pfeil ausgegraut.</summary>
        protected virtual bool CanStep(int delta)
        {
            return true;
        }

        /// <summary>Ein Klick auf den Inhalt (nicht auf die Pfeile); das Feld hat dann schon den Fokus.</summary>
        protected virtual void OnContentClick(Point location)
        {
        }

        /// <summary>Zeichnet den Inhalt ab <see cref="TextLeft"/>; <paramref name="fore"/> ist die Textfarbe (ausgegraut, wenn gesperrt).</summary>
        protected abstract void PaintContent(Graphics g, Color fore);

        /// <summary>Gruener Rand: mit dem Fokus (beim Datumsfeld auch, solange sein Kalender offen ist).</summary>
        protected virtual bool ShowsFocus
        {
            get { return Focused; }
        }

        /// <summary>
        /// Groesse passend zu Schrift und Inhalt, so hoch wie die runden Wochentag-Knoepfe im Zeitplan. Die abgeleiteten
        /// Felder rufen das am Ende ihres Konstruktors auf.
        /// </summary>
        protected void Fit()
        {
            int height = Math.Max(Dpi.Px(32), Font.Height + Dpi.Px(12));
            Size = new Size(Math.Max(Dpi.Px(84), ContentWidth + Dpi.Px(48)), height);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Fit();
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
                OnContentClick(e.Location);
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
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            StopRepeat();
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        // ------------------------------------------------------------------ Zeichnen

        /// <summary>Spalte der beiden Pfeile am rechten Rand.</summary>
        protected Rectangle ArrowBounds
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
            Color border = enabled && ShowsFocus ? Theme.Green : Theme.FieldBorder;
            Metrics.PaintRounded(g, ClientRectangle, Dpi.Px(6), enabled ? Theme.InputBack : Theme.Track, border);
            PaintContent(g, enabled ? Theme.Text : Theme.Disabled);

            Rectangle arrows = ArrowBounds;
            int middle = arrows.Top + arrows.Height / 2;
            int center = arrows.Left + arrows.Width / 2;
            DrawArrow(g, center, middle - Dpi.Px(4), true, ArrowColor(1));
            DrawArrow(g, center, middle + Dpi.Px(4), false, ArrowColor(-1));
        }

        Color ArrowColor(int arrow)
        {
            if (!Enabled || !CanStep(arrow)) return Theme.Disabled;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing) _repeat.Dispose();
            base.Dispose(disposing);
        }
    }
}
