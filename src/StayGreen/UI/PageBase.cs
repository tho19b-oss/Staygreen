using System;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Gemeinsame Basis der Registerkarten: bindet die Einstellungen, unterdrueckt Aenderungsereignisse beim
    /// Befuellen der Felder und stellt die uebliche Zeilen-Anordnung bereit.
    /// </summary>
    abstract class PageBase : UserControl
    {
        bool _loading;

        protected PageBase()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
        }

        protected Settings S { get; private set; }

        /// <summary>Wird ausgeloest, wenn der Nutzer etwas geaendert hat (nicht beim Befuellen der Felder).</summary>
        public event Action Changed;

        protected bool Loading
        {
            get { return _loading; }
        }

        protected void Fire()
        {
            if (!_loading && S != null && Changed != null) Changed();
        }

        /// <summary>Fuehrt <paramref name="action"/> aus, ohne dass Aenderungsereignisse als Nutzereingabe zaehlen.</summary>
        protected void Quiet(Action action)
        {
            bool previous = _loading;
            _loading = true;
            try { action(); }
            finally { _loading = previous; }
        }

        public void LoadSettings(Settings settings)
        {
            S = settings;
            Quiet(() => Populate(settings));
        }

        protected abstract void Populate(Settings settings);

        /// <summary>Setzt alle Beschriftungen in der aktuellen Sprache.</summary>
        public abstract void ApplyTexts();

        /// <summary>Jede Sekunde, solange die Karte sichtbar ist (fuer "naechster Start ..."-Zeilen).</summary>
        public virtual void Tick(DateTime now, DateTime? autoStopDue)
        {
        }

        // ---------------------------------------------------------------- Layout-Helfer

        TableLayoutPanel _root;

        /// <summary>
        /// Senkrechte Hauptspalte der Karte. Ihre Hoehe wird nicht ueber AutoSize bestimmt, sondern bei jeder
        /// Groessen- oder Textaenderung explizit aus der tatsaechlichen Breite berechnet; so stimmt die Hoehe
        /// auch dann, wenn umbrechende Hinweistexte erst nach dem ersten Layout ihre endgueltige Breite haben.
        /// </summary>
        protected TableLayoutPanel CreateRoot()
        {
            _root = new TableLayoutPanel
            {
                AutoSize = false,
                Dock = DockStyle.None,
                ColumnCount = 1,
                Padding = new Padding(8),
            };
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            Controls.Add(_root);
            return _root;
        }

        /// <summary>Hoehe der Hauptspalte neu berechnen (nach geaenderten Texten oder Sichtbarkeiten).</summary>
        public void RefreshLayout()
        {
            if (_root == null || Width <= 0) return;

            // Reicht die Hoehe nicht, erscheint ein senkrechter Balken. Seine Breite gleich von vornherein abziehen,
            // sonst ragt die Spalte danach seitlich heraus und es entsteht zusaetzlich ein waagerechter Balken.
            int width = Math.Max(120, Width);
            int height = _root.GetPreferredSize(new Size(width, 0)).Height;
            if (height > Height)
            {
                width = Math.Max(120, Width - SystemInformation.VerticalScrollBarWidth);
                height = _root.GetPreferredSize(new Size(width, 0)).Height;
            }
            _root.SetBounds(0, 0, width, height);
            AutoScrollMinSize = new Size(0, height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RefreshLayout();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) RefreshLayout();
        }

        protected static void AddRow(TableLayoutPanel table, Control control)
        {
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(3, 4, 3, 4);
            table.Controls.Add(control);
        }

        protected static GroupBox CreateGroup(Control content)
        {
            var group = new GroupBox
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 4, 8, 8),
            };
            content.Dock = DockStyle.Top;
            group.Controls.Add(content);
            return group;
        }

        protected static TableLayoutPanel CreateGrid(int columns)
        {
            var grid = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = columns,
            };
            return grid;
        }

        protected static Label CreateLabel()
        {
            return new Label { AutoSize = true, Anchor = AnchorStyles.Left, UseMnemonic = false, Margin = new Padding(3, 6, 6, 6) };
        }

        protected static WrapLabel CreateHint()
        {
            return new WrapLabel { ForeColor = Theme.Muted };
        }

        protected static CheckBox CreateCheck()
        {
            return new CheckBox { AutoSize = true, UseMnemonic = false };
        }
    }
}
