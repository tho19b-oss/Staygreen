using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StayGreen.UI
{
    /// <summary>Pixelwerte in Abhaengigkeit von der Windows-Skalierung (100 %, 125 %, 150 % ...).</summary>
    static class Dpi
    {
        static float _factor;

        public static float Factor
        {
            get
            {
                if (_factor <= 0f)
                {
                    try
                    {
                        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                            _factor = Math.Max(1f, g.DpiX / 96f);
                    }
                    catch
                    {
                        _factor = 1f;
                    }
                }
                return _factor;
            }
        }

        public static int Px(int value)
        {
            return (int)Math.Round(value * Factor);
        }
    }

    /// <summary>Eine Komponente, die ihre Hoehe fuer eine gegebene Breite selbst berechnen kann.</summary>
    interface IMeasurable
    {
        int MeasureHeight(int width);
    }

    /// <summary>Text vermessen und abgerundete Flaechen zeichnen.</summary>
    static class Metrics
    {
        const TextFormatFlags WrapFlags =
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        public static int TextHeight(string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text)) text = " ";
            return TextRenderer.MeasureText(text, font, new Size(Math.Max(1, width), 0), WrapFlags).Height;
        }

        public static int TextWidth(string text, Font font)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return TextRenderer.MeasureText(text, font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
        }

        /// <summary>Hoehe eines Kindes bei gegebener Breite: selbst berechnet, bevorzugt oder die feste Hoehe.</summary>
        public static int Measure(Control c, int width)
        {
            var measurable = c as IMeasurable;
            if (measurable != null) return measurable.MeasureHeight(width);
            if (c.AutoSize) return c.GetPreferredSize(new Size(width, 0)).Height;
            return c.Height;
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Gefuelltes Rechteck mit runden Ecken und optionalem Rand (Color.Empty = weglassen).</summary>
        public static void PaintRounded(Graphics g, Rectangle bounds, int radius, Color fill, Color border)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (GraphicsPath path = RoundRect(r, radius))
            {
                if (fill != Color.Empty)
                    using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                if (border != Color.Empty)
                    using (var pen = new Pen(border)) g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Basis aller selbst angeordneten Container. Die Hoehe wird von oben nach unten bestimmt: Der Eltern-Container
    /// fragt <see cref="MeasureHeight"/> mit der verfuegbaren Breite ab und legt die Groesse fest; erst dann ordnet
    /// sich der Container selbst an. Es gibt keine AutoSize-Ketten und keine Rueckkopplung.
    /// </summary>
    abstract class LayoutPanel : Panel, IMeasurable
    {
        bool _topDivider;
        bool _inLayout;

        protected LayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        /// <summary>Innenabstand in logischen Pixeln (wird mit der Windows-Skalierung multipliziert).</summary>
        public Padding Inset { get; set; }

        /// <summary>Trennlinie am oberen Rand (zwischen den Zeilen einer Karte).</summary>
        public bool TopDivider
        {
            get { return _topDivider; }
            set
            {
                if (_topDivider == value) return;
                _topDivider = value;
                Invalidate();
            }
        }

        public abstract int MeasureHeight(int width);

        protected abstract void DoLayout();

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);

            // Das Setzen der Kind-Groessen loest erneut ein Layout dieses Containers aus; das aeussere DoLayout
            // platziert ohnehin alle Kinder, deshalb genuegt es, die verschachtelte Anfrage zu ignorieren.
            if (_inLayout) return;
            _inLayout = true;
            try { DoLayout(); }
            finally { _inLayout = false; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_topDivider)
                using (var pen = new Pen(Theme.Divider))
                    e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
    }

    /// <summary>Senkrechte Spalte: jedes sichtbare Kind bekommt die volle Breite und seine eigene Hoehe.</summary>
    class Stack : LayoutPanel
    {
        /// <summary>Abstand zwischen den Kindern in logischen Pixeln.</summary>
        public int Gap { get; set; }

        public override int MeasureHeight(int width)
        {
            int inner = Math.Max(1, width - Dpi.Px(Inset.Horizontal));
            int gap = Dpi.Px(Gap);
            int total = Dpi.Px(Inset.Vertical);
            bool first = true;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                if (!first) total += gap;
                first = false;
                total += Metrics.Measure(c, inner);
            }
            return total;
        }

        protected override void DoLayout()
        {
            int left = Dpi.Px(Inset.Left);
            int inner = Math.Max(1, ClientSize.Width - Dpi.Px(Inset.Horizontal));
            int gap = Dpi.Px(Gap);
            int y = Dpi.Px(Inset.Top);
            bool first = true;
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                if (!first) y += gap;
                first = false;
                int h = Metrics.Measure(c, inner);
                PlaceChild(c, new Rectangle(left, y, inner, h));
                y += h;
            }
        }

        /// <summary>Setzt die Groesse; aendert sie sich nicht, wird der Inhalt trotzdem neu angeordnet (Texte koennen sich geaendert haben).</summary>
        internal static void PlaceChild(Control c, Rectangle bounds)
        {
            if (c.Bounds != bounds) c.Bounds = bounds;
            else if (c is IMeasurable) c.PerformLayout();
        }
    }

    /// <summary>Weisse Karte mit Rand und abgerundeten Ecken, oben optional eine Ueberschrift.</summary>
    sealed class Card : Stack
    {
        readonly WrapLabel _title = new WrapLabel();
        int _rows;

        public Card()
        {
            BackColor = Theme.Card;
            Inset = new Padding(16, 14, 16, 12);
            _title.ForeColor = Theme.Text;
            _title.Padding = new Padding(0, 0, 0, Dpi.Px(4));
            UpdateTitleFont();
            Controls.Add(_title);
        }

        public override string Text
        {
            get { return _title.Text; }
            set
            {
                _title.Text = value;
                _title.Visible = !string.IsNullOrEmpty(value);
            }
        }

        /// <summary>Fuegt eine Zeile hinzu; ab der zweiten Zeile mit Trennlinie darueber.</summary>
        public T AddRow<T>(T row) where T : LayoutPanel
        {
            row.TopDivider = _rows++ > 0;
            Controls.Add(row);
            return row;
        }

        /// <summary>Fuegt ein Element ohne Trennlinie hinzu.</summary>
        public T Add<T>(T control) where T : Control
        {
            _rows++;
            Controls.Add(control);
            return control;
        }

        void UpdateTitleFont()
        {
            Font old = _title.Font;
            _title.Font = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold);
            if (old != null && !ReferenceEquals(old, Font)) old.Dispose();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateTitleFont();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Background);
            Metrics.PaintRounded(e.Graphics, ClientRectangle, Dpi.Px(12), Theme.Card, Theme.CardBorder);
        }
    }

    /// <summary>
    /// Eine Einstellung: links Titel und erklaerender Text, rechts ein Bedienelement (Schalter, Auswahl, Knopf ...).
    /// Ein Klick auf den Text schaltet einen Schalter um.
    /// </summary>
    sealed class SettingRow : LayoutPanel
    {
        readonly WrapLabel _title = new WrapLabel();
        readonly WrapLabel _caption = new WrapLabel();
        readonly Control _accessory;

        public SettingRow(Control accessory)
        {
            _accessory = accessory;
            Inset = new Padding(0, 10, 0, 10);
            _title.ForeColor = Theme.Text;
            _caption.ForeColor = Theme.Muted;
            Controls.Add(_title);
            Controls.Add(_caption);
            if (accessory != null) Controls.Add(accessory);

            if (accessory is SwitchBox)
            {
                Cursor = Cursors.Hand;
                _title.Click += (o, e) => Toggle();
                _caption.Click += (o, e) => Toggle();
                Click += (o, e) => Toggle();
            }
        }

        public string Title
        {
            get { return _title.Text; }
            set
            {
                _title.Text = value ?? "";
                if (_accessory is SwitchBox || _accessory is ComboBox) _accessory.AccessibleName = _title.Text;
            }
        }

        public string Caption
        {
            get { return _caption.Text; }
            set
            {
                _caption.Text = value ?? "";
                _caption.Visible = _caption.Text.Length > 0;
            }
        }

        public Color CaptionColor
        {
            get { return _caption.ForeColor; }
            set { _caption.ForeColor = value; }
        }

        void Toggle()
        {
            var box = _accessory as CheckBox;
            if (box != null && box.Enabled) box.Checked = !box.Checked;
        }

        int AccessoryWidth
        {
            get
            {
                if (_accessory == null) return 0;
                var inline = _accessory as InlineRow;
                return inline != null ? inline.PreferredWidth : _accessory.Width;
            }
        }

        int AccessoryHeight(int width)
        {
            return _accessory == null ? 0 : Metrics.Measure(_accessory, width);
        }

        int TextHeight(int textWidth)
        {
            int h = _title.MeasureHeight(textWidth);
            if (_caption.Text.Length > 0) h += _caption.MeasureHeight(textWidth);
            return h;
        }

        int TextWidth(int width)
        {
            int aw = AccessoryWidth;
            return Math.Max(Dpi.Px(80), width - (aw > 0 ? aw + Dpi.Px(16) : 0));
        }

        public override int MeasureHeight(int width)
        {
            int content = Math.Max(TextHeight(TextWidth(width)), AccessoryHeight(AccessoryWidth));
            return content + Dpi.Px(Inset.Vertical);
        }

        protected override void DoLayout()
        {
            int width = ClientSize.Width;
            int top = Dpi.Px(Inset.Top);
            int tw = TextWidth(width);

            int titleH = _title.MeasureHeight(tw);
            _title.SetBounds(0, top, tw, titleH);
            int textH = titleH;
            if (_caption.Text.Length > 0)
            {
                int capH = _caption.MeasureHeight(tw);
                _caption.SetBounds(0, top + titleH, tw, capH);
                textH += capH;
            }

            if (_accessory != null)
            {
                int aw = AccessoryWidth;
                int ah = AccessoryHeight(aw);
                int rowH = Math.Max(textH, ah);
                PlaceAccessory(new Rectangle(width - aw, top + (rowH - ah) / 2, aw, ah));
            }
        }

        void PlaceAccessory(Rectangle bounds)
        {
            Stack.PlaceChild(_accessory, bounds);
        }
    }

    /// <summary>Waagerechte Reihe von Bedienelementen, links beginnend und senkrecht zentriert.</summary>
    sealed class InlineRow : LayoutPanel
    {
        public InlineRow(params Control[] items)
        {
            Gap = 8;
            foreach (Control c in items) Controls.Add(c);
        }

        /// <summary>Abstand zwischen den Elementen in logischen Pixeln.</summary>
        public int Gap { get; set; }

        /// <summary>Beschriftungen messen sich selbst, alles andere behaelt die vom Ersteller gesetzte Groesse.</summary>
        static Size SizeOf(Control c)
        {
            return c is Label && c.AutoSize ? c.GetPreferredSize(Size.Empty) : c.Size;
        }

        public int PreferredWidth
        {
            get
            {
                int total = 0;
                bool first = true;
                foreach (Control c in Controls)
                {
                    if (!c.Visible) continue;
                    if (!first) total += Dpi.Px(Gap);
                    first = false;
                    total += SizeOf(c).Width;
                }
                return total;
            }
        }

        public override int MeasureHeight(int width)
        {
            int h = 0;
            foreach (Control c in Controls)
                if (c.Visible) h = Math.Max(h, SizeOf(c).Height);
            return h + Dpi.Px(Inset.Vertical);
        }

        protected override void DoLayout()
        {
            int x = 0;
            bool first = true;
            int innerH = Math.Max(0, Height - Dpi.Px(Inset.Vertical));
            foreach (Control c in Controls)
            {
                if (!c.Visible) continue;
                if (!first) x += Dpi.Px(Gap);
                first = false;
                Size s = SizeOf(c);
                c.SetBounds(x, Dpi.Px(Inset.Top) + (innerH - s.Height) / 2, s.Width, s.Height);
                x += s.Width;
            }
        }
    }

    /// <summary>Rahmen um ein einzelnes Steuerelement (z. B. die Liste der Zeitfenster).</summary>
    sealed class Frame : Panel
    {
        public Frame()
        {
            Padding = new Padding(1);
            BackColor = Theme.Card;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Metrics.PaintRounded(e.Graphics, ClientRectangle, Dpi.Px(6), Color.Empty, Theme.FieldBorder);
        }
    }

    /// <summary>Scrollbarer Bereich fuer eine Registerkarte; ordnet seinen Inhalt auf die verfuegbare Breite an.</summary>
    sealed class ScrollHost : Panel
    {
        bool _busy;

        public ScrollHost(Stack content)
        {
            Content = content;
            DoubleBuffered = true;
            BackColor = Theme.Background;

            // Nie waagerecht scrollen: der Inhalt passt sich immer der Breite an.
            AutoScroll = false;
            HorizontalScroll.Enabled = false;
            HorizontalScroll.Visible = false;
            HorizontalScroll.Maximum = 0;
            AutoScroll = true;

            Controls.Add(content);
        }

        public Stack Content { get; private set; }

        /// <summary>
        /// Ordnet den Inhalt auf die tatsaechlich sichtbare Breite an. Erscheint oder verschwindet dadurch der
        /// senkrechte Balken, aendert sich die sichtbare Breite; dann wird noch einmal angeordnet. So entsteht nie
        /// ein waagerechter Balken.
        /// </summary>
        public void Relayout()
        {
            if (_busy || !Visible || ClientSize.Width <= 0) return;
            _busy = true;
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    int width = ClientSize.Width;
                    int height = Content.MeasureHeight(width);

                    var bounds = new Rectangle(0, AutoScrollPosition.Y, width, height);
                    bool sizeChanged = Content.Size != bounds.Size;
                    Content.Bounds = bounds;
                    if (!sizeChanged) Content.PerformLayout();
                    AutoScrollMinSize = new Size(0, height);

                    if (ClientSize.Width == width) break;
                }
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Scrollt um einen Mausrad-Schritt (positiv = nach oben).</summary>
        public void ScrollByWheel(int delta)
        {
            int target = -AutoScrollPosition.Y - delta * Dpi.Px(48) / 120;
            AutoScrollPosition = new Point(0, Math.Max(0, target));
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Relayout();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            Relayout();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) Relayout();
        }
    }
}
