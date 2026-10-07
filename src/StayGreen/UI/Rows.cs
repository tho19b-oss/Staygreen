using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>Ein Eintrag einer <see cref="ListStack"/>: Die Liste schaltet seine Trennlinie und richtet die Bezeichnungen aus.</summary>
    interface IListItem
    {
        /// <summary>Trennlinie unter dem Eintrag.</summary>
        bool Divider { get; set; }

        /// <summary>Der Knopf mit Bezeichnung und Wert, dessen Bezeichnungs-Spalte die Liste ausrichtet.</summary>
        ValueButton ValueButton { get; }
    }

    /// <summary>
    /// Der Knopf einer Wertzeile: links die Bezeichnung (z. B. "Taste" oder "Mo-Fr"), daneben der Wert in groesserer,
    /// halbfetter Schrift, bei Bedarf eine Kennzeichnung ("ueber Nacht", "zu lang") und rechts ein Stift als Hinweis, dass
    /// ein Klick (oder Leertaste/Eingabe) die Zeile an ihrer Stelle zum Bearbeiten oeffnet.
    /// </summary>
    sealed class ValueButton : PaintedButton
    {
        string _label = "";
        string _value = "";
        string _pill = "";
        bool _warn;
        int _labelWidth;
        Font _valueFont;
        Font _pillFont;

        public ValueButton()
        {
            UpdateFonts();
        }

        /// <summary>Breite der Bezeichnungs-Spalte in Pixeln; in einer Liste fuer alle Zeilen gleich, damit die Werte untereinander stehen.</summary>
        public int LabelWidth
        {
            get { return _labelWidth; }
            set
            {
                if (_labelWidth == value) return;
                _labelWidth = value;
                Invalidate();
            }
        }

        /// <summary>Ein langer Wert bricht um (z. B. eine Aufzaehlung); sonst endet er mit "...".</summary>
        public bool WrapValue { get; set; }

        /// <summary>Platz, den die Bezeichnung braucht.</summary>
        public int LabelTextWidth
        {
            get { return Metrics.TextWidth(_label, Font); }
        }

        /// <summary>Setzt den Inhalt; <paramref name="warn"/> toent die Kennzeichnung rot (z. B. "zu lang").</summary>
        public void SetContent(string label, string value, string pill, bool warn)
        {
            label = label ?? "";
            value = value ?? "";
            pill = pill ?? "";
            if (_label == label && _value == value && _pill == pill && _warn == warn) return;
            _label = label;
            _value = value;
            _pill = pill;
            _warn = warn;
            Invalidate();
        }

        /// <summary>Hoehe fuer eine gegebene Breite: mindestens 44 Pixel, mehr fuer einen umbrochenen Wert.</summary>
        public int MeasureHeight(int width)
        {
            int min = Math.Max(Dpi.Px(44), _valueFont.Height + Dpi.Px(18));
            int available = ValueSpace(width);
            if (!Wraps(available)) return min;
            return Math.Max(min, Metrics.TextHeight(_value, _valueFont, available) + Dpi.Px(18));
        }

        /// <summary>Linker Rand des Werts.</summary>
        int ValueLeft(int width)
        {
            int x = Dpi.Px(8);
            return x + Math.Min(_labelWidth, Math.Max(Dpi.Px(40), (PencilLeft(width) - x) / 2)) + Dpi.Px(12);
        }

        static int PencilLeft(int width)
        {
            return width - Dpi.Px(10) - Dpi.Px(IconPainter.Size);
        }

        /// <summary>Breite, die der Wert (samt Kennzeichnung) hoechstens einnehmen darf.</summary>
        int ValueSpace(int width)
        {
            return Math.Max(1, PencilLeft(width) - Dpi.Px(8) - ValueLeft(width));
        }

        bool Wraps(int available)
        {
            return WrapValue && _pill.Length == 0 && Metrics.TextWidth(_value, _valueFont) > available;
        }

        void UpdateFonts()
        {
            Font oldValue = _valueFont;
            Font oldPill = _pillFont;
            _valueFont = Fonts.Semibold(Font, 1.5f);
            _pillFont = Fonts.Smaller(Font, 0.75f);
            if (oldValue != null) oldValue.Dispose();
            if (oldPill != null) oldPill.Dispose();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateFonts();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Background(g, this);
            if (Hover || Down)
                Metrics.PaintRounded(g, ClientRectangle, Dpi.Px(8), Down ? Theme.Track : Theme.Chip, Color.Empty);

            int x = Dpi.Px(8);
            int valueLeft = ValueLeft(Width);
            Draw.LeftText(g, _label, Font, new Rectangle(x, 0, Math.Max(1, valueLeft - Dpi.Px(12) - x), Height), Theme.MutedStrong);

            int available = ValueSpace(Width);
            if (Wraps(available))
            {
                int textHeight = Metrics.TextHeight(_value, _valueFont, available);
                Draw.WrappedText(g, _value, _valueFont, new Rectangle(valueLeft, (Height - textHeight) / 2, available, textHeight), Theme.Text);
            }
            else
            {
                int valueWidth = Math.Min(Metrics.TextWidth(_value, _valueFont), available);
                Draw.LeftText(g, _value, _valueFont, new Rectangle(valueLeft, 0, valueWidth, Height), Theme.Text);
                int pillLeft = valueLeft + valueWidth + Dpi.Px(10);
                if (_pill.Length > 0)
                {
                    Size pill = PillPainter.Measure(_pill, _pillFont);
                    if (pillLeft + pill.Width <= PencilLeft(Width) - Dpi.Px(8))
                        PillPainter.Paint(g, new Rectangle(pillLeft, (Height - pill.Height) / 2, pill.Width, pill.Height), _pill, _pillFont, _warn);
                }
            }

            int icon = Dpi.Px(IconPainter.Size);
            IconPainter.Draw(g, IconKind.Pencil, new Rectangle(PencilLeft(Width), (Height - icon) / 2, icon, icon),
                Hover || Down ? Theme.Text : Theme.Muted);
            if (Focused && ShowFocusCues)
                Draw.FocusRing(g, Rectangle.Inflate(ClientRectangle, -Dpi.Px(2), -Dpi.Px(2)), Dpi.Px(6));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_valueFont != null) _valueFont.Dispose();
                if (_pillFont != null) _pillFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Eine Wertzeile: der <see cref="ValueButton"/> ueber die ganze Breite, bei Bedarf rechts ein Muelleimer.</summary>
    sealed class ValueRow : LayoutPanel, IListItem
    {
        public ValueRow(bool removable)
        {
            Inset = new Padding(0, 4, 0, 4);
            BottomDivider = true;
            Controls.Add(Button);
            if (removable)
            {
                RemoveButton = new IconButton { Icon = IconKind.Trash };
                Controls.Add(RemoveButton);
            }
        }

        public ValueButton Button { get; } = new ValueButton();

        /// <summary>Der Muelleimer (nur bei entfernbaren Zeilen, sonst null).</summary>
        public IconButton RemoveButton { get; }

        public bool Divider
        {
            get { return BottomDivider; }
            set { BottomDivider = value; }
        }

        ValueButton IListItem.ValueButton
        {
            get { return Button; }
        }

        int ButtonWidth(int width)
        {
            return RemoveButton == null ? width : Math.Max(1, width - RemoveButton.Width - Dpi.Px(4));
        }

        public override int MeasureHeight(int width)
        {
            return Button.MeasureHeight(ButtonWidth(width)) + Dpi.Px(Inset.Vertical);
        }

        protected override void DoLayout()
        {
            int top = Dpi.Px(Inset.Top);
            int height = Math.Max(1, ClientSize.Height - Dpi.Px(Inset.Vertical));
            Button.SetBounds(0, top, ButtonWidth(ClientSize.Width), height);
            if (RemoveButton != null)
            {
                Size remove = RemoveButton.Size;
                RemoveButton.SetBounds(ClientSize.Width - remove.Width, top + (height - remove.Height) / 2, remove.Width, remove.Height);
            }
        }
    }

    /// <summary>
    /// Liste von Wertzeilen unter einer Trennlinie (unter dem Kartenkopf). Alle Bezeichnungen bekommen dieselbe Breite,
    /// damit die Werte untereinander stehen. Ohne <see cref="TrailingDivider"/> endet die Liste ohne Linie (wenn in der
    /// Karte nichts mehr folgt).
    /// </summary>
    sealed class ListStack : Stack
    {
        public ListStack()
        {
            // Ein Pixel Abstand oben, damit die erste Zeile die Trennlinie nicht ueberdeckt.
            TopDivider = true;
            Inset = new Padding(0, 1, 0, 0);
            TrailingDivider = true;
        }

        /// <summary>Auch unter der letzten Zeile eine Linie (wenn darunter noch etwas folgt, z. B. ein Knopf).</summary>
        public bool TrailingDivider { get; set; }

        public override int MeasureHeight(int width)
        {
            Prepare();
            return base.MeasureHeight(width);
        }

        protected override void DoLayout()
        {
            Prepare();
            base.DoLayout();
        }

        /// <summary>Gemeinsame Breite der Bezeichnungen (auch verborgener Zeilen, damit nichts springt) und die Trennlinien.</summary>
        void Prepare()
        {
            int labels = Dpi.Px(84);
            IListItem last = null;
            foreach (Control c in Controls)
            {
                var item = c as IListItem;
                if (item == null) continue;
                if (item.ValueButton != null) labels = Math.Max(labels, item.ValueButton.LabelTextWidth);
                if (!c.Visible) continue;
                item.Divider = true;
                last = item;
            }
            if (last != null && !TrailingDivider) last.Divider = false;

            foreach (Control c in Controls)
            {
                var item = c as IListItem;
                if (item != null && item.ValueButton != null) item.ValueButton.LabelWidth = labels;
            }
        }
    }

    /// <summary>Wie der Editor einer <see cref="EditableRow"/> Werte uebernimmt.</summary>
    enum EditorKind
    {
        /// <summary>Eine Auswahl, die sofort gilt: Ein Klick auf eine Moeglichkeit uebernimmt sie und klappt die Zeile zu.</summary>
        Choice,

        /// <summary>Eingabefelder mit Speichern und Abbrechen; Eingabe speichert, Esc bricht ab.</summary>
        Input,
    }

    /// <summary>
    /// Eine Einstellung als Wertzeile, die sich per Klick an ihrer Stelle zum Editor aufklappt, wie die Zeitfenster im
    /// Zeitplan. Die Seite fuellt den Editor beim Oeffnen (<see cref="Opening"/>) und uebernimmt die Werte selbst: bei einer
    /// Auswahl, sobald gewaehlt wurde, bei einer Eingabe auf <see cref="SaveRequested"/>. Danach ruft sie
    /// <see cref="Close"/>. Esc klappt den Editor ohne Aenderung zu.
    /// </summary>
    sealed class EditableRow : Stack, IListItem
    {
        readonly Stack _editorRow = new Stack { Inset = new Padding(0, 10, 0, 10), BottomDivider = true };
        readonly TintBox _box = new TintBox { Gap = 10 };
        readonly WrapLabel _title = new WrapLabel();
        readonly WrapLabel _problem = new WrapLabel();
        readonly FlatButton _save = new FlatButton { Kind = ButtonKind.Primary };
        readonly FlatButton _cancel = new FlatButton { Kind = ButtonKind.Secondary };
        readonly FlowRow _buttons;
        readonly EditorKind _kind;
        int _content;
        bool _open;

        public EditableRow(EditorKind kind)
        {
            _kind = kind;
            Row = new ValueRow(false);
            Controls.Add(Row);

            _title.ForeColor = Theme.Text;
            _problem.ForeColor = Theme.WarnText;
            _problem.Visible = false;
            _box.AccessibleRole = AccessibleRole.Grouping;
            _buttons = new FlowRow(_save, _cancel) { Visible = kind == EditorKind.Input };
            _box.Controls.Add(_title);
            _box.Controls.Add(_problem);
            _box.Controls.Add(_buttons);
            _editorRow.Controls.Add(_box);
            _editorRow.Visible = false;
            Controls.Add(_editorRow);
            UpdateFonts();

            Row.Button.Click += (o, e) => Open();
            _save.Click += (o, e) => RequestSave();
            _cancel.Click += (o, e) => Close();
        }

        /// <summary>Die Seite fuellt jetzt den Editor mit den aktuellen Werten.</summary>
        public event Action Opening;

        /// <summary>Der Editor ist aufgeklappt (die Seite schliesst dann andere Editoren).</summary>
        public event Action Opened;

        /// <summary>"Speichern" oder Eingabe: Die Seite prueft und uebernimmt die Werte und ruft dann <see cref="Close"/>.</summary>
        public event Action SaveRequested;

        public ValueRow Row { get; private set; }

        public bool IsOpen
        {
            get { return _open; }
        }

        /// <summary>Wohin der Fokus beim Oeffnen springt (sonst auf das erste Bedienelement des Editors).</summary>
        public Control FocusTarget { get; set; }

        public bool Divider
        {
            get { return Row.BottomDivider; }
            set
            {
                Row.BottomDivider = value;
                _editorRow.BottomDivider = value;
            }
        }

        ValueButton IListItem.ValueButton
        {
            get { return Row.Button; }
        }

        /// <summary>Titel des Editors (fett), zugleich sein Name fuer Screenreader.</summary>
        public string EditorTitle
        {
            get { return _title.Text; }
            set
            {
                _title.Text = value ?? "";
                _box.AccessibleName = _title.Text;
            }
        }

        /// <summary>Beschriftungen der Knoepfe eines Eingabe-Editors.</summary>
        public void SetButtonTexts(string save, string cancel)
        {
            _save.Text = save;
            _cancel.Text = cancel;
        }

        /// <summary>Zeigt Bezeichnung und Wert in der Zeile; eine Kennzeichnung ist optional.</summary>
        public void SetValue(string label, string value, string pill = "", bool warn = false)
        {
            pill = pill ?? "";
            Row.Button.SetContent(label, value, pill, warn);
            string spoken = label + ": " + value + (pill.Length > 0 ? ", " + pill : "");
            Row.Button.AccessibleName = Loc.T("row.edit", spoken);
        }

        /// <summary>Langer Wert bricht um, statt mit "..." zu enden.</summary>
        public bool WrapValue
        {
            get { return Row.Button.WrapValue; }
            set { Row.Button.WrapValue = value; }
        }

        /// <summary>Fuegt dem Editor ein Element hinzu (unter dem Titel, ueber dem Hinweis und den Knoepfen).</summary>
        public T AddContent<T>(T control) where T : Control
        {
            _box.Controls.Add(control);
            _box.Controls.SetChildIndex(control, 1 + _content++);

            // Tab-Reihenfolge wie auf dem Bildschirm: erst die Felder, dann Speichern und Abbrechen.
            for (int i = 0; i < _box.Controls.Count; i++) _box.Controls[i].TabIndex = i;
            return control;
        }

        /// <summary>Warum sich die Eingabe nicht speichern laesst; leer, wenn alles passt. Sperrt so lange "Speichern".</summary>
        public string Problem
        {
            get { return _problem.Text; }
            set
            {
                string text = value ?? "";
                bool show = text.Length > 0;
                if (_problem.Text == text && _problem.Visible == show) return;
                _problem.Text = text;
                _problem.Visible = show;
                _save.Enabled = !show;
                _save.AccessibleDescription = show ? text : null;
                RefreshHost();
            }
        }

        /// <summary>Klappt den Editor auf (die Seite fuellt ihn in <see cref="Opening"/>) und setzt den Fokus hinein.</summary>
        public void Open()
        {
            if (!_open)
            {
                Action opening = Opening;
                if (opening != null) opening();
                _open = true;
                Action opened = Opened;
                if (opened != null) opened();
            }

            // Erst zeigen, dann den Fokus setzen, erst danach die Zeile verbergen: So springt der Fokus nie auf ein ganz
            // anderes Element (Windows wuerde die Seite sonst dorthin rollen).
            _editorRow.Visible = true;
            Control target = FocusTarget != null && FocusTarget.Visible ? FocusTarget : FirstFocusable(_box);
            if (target != null) target.Focus();
            Row.Visible = false;
            RefreshHost();
            ScrollIntoView(_editorRow);
        }

        /// <summary>Klappt den Editor zu; hatte er den Fokus, geht er an die Zeile zurueck.</summary>
        public void Close()
        {
            if (!_open) return;
            bool hadFocus = _editorRow.ContainsFocus;
            _open = false;
            Row.Visible = true;
            if (hadFocus) Row.Button.Focus();
            _editorRow.Visible = false;
            RefreshHost();
        }

        void RequestSave()
        {
            if (!_open || !_save.Enabled) return;
            Action handler = SaveRequested;
            if (handler != null) handler();
        }

        /// <summary>Im Editor: Esc klappt zu; bei einer Eingabe speichert Eingabe (auf einem Knopf loest Eingabe diesen Knopf aus).</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_open && _editorRow.ContainsFocus)
            {
                Control focused = FocusedIn(_box);
                var combo = focused as ComboBox;
                bool listOpen = combo != null && combo.DroppedDown;
                if (keyData == Keys.Escape && !listOpen)
                {
                    Close();
                    return true;
                }
                if (keyData == Keys.Enter && _kind == EditorKind.Input && !listOpen && !(focused is Button))
                {
                    RequestSave();
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        static Control FocusedIn(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c.Focused) return c;
                if (c.ContainsFocus) return FocusedIn(c) ?? c;
            }
            return null;
        }

        static Control FirstFocusable(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (!c.Visible || !c.Enabled) continue;
                if (c.CanSelect && c.TabStop) return c;
                Control inner = FirstFocusable(c);
                if (inner != null) return inner;
            }
            return null;
        }

        ScrollHost Host
        {
            get
            {
                for (Control c = Parent; c != null; c = c.Parent)
                {
                    var host = c as ScrollHost;
                    if (host != null) return host;
                }
                return null;
            }
        }

        void RefreshHost()
        {
            ScrollHost host = Host;
            if (host != null) host.Relayout();
        }

        void ScrollIntoView(Control control)
        {
            ScrollHost host = Host;
            if (host != null) host.ScrollControlIntoView(control);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateFonts();
        }

        void UpdateFonts()
        {
            Font old = _title.Font;
            _title.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
            if (old != null && !ReferenceEquals(old, Font)) old.Dispose();
        }
    }

    /// <summary>
    /// Genau eine von vielen Moeglichkeiten als umbrechende Reihe runder Knoepfe (z. B. die Tasten F13 bis F24). Ein Klick,
    /// die Leertaste oder Eingabe waehlt (<see cref="Picked"/>); die Pfeiltasten wandern nur zwischen den Knoepfen. Allein
    /// der gewaehlte Knopf ist ein Tabstopp, damit Tab die Reihe in einem Schritt durchquert.
    /// </summary>
    sealed class ChoiceChips : Stack
    {
        readonly FlowRow _flow = new FlowRow();
        readonly List<ChipBox> _chips = new List<ChipBox>();
        int _selected = -1;

        public ChoiceChips()
        {
            AccessibleRole = AccessibleRole.Grouping;
            Controls.Add(_flow);
        }

        /// <summary>Der Nutzer hat eine Moeglichkeit gewaehlt (auch die schon gewaehlte).</summary>
        public event EventHandler Picked;

        /// <summary>Beschriftungen der Knoepfe (Anzahl und Reihenfolge bleiben gleich, nur die Sprache kann wechseln).</summary>
        public void SetItems(IList<string> items)
        {
            while (_chips.Count < items.Count)
            {
                var chip = new ChipBox { AutoCheck = false, AccessibleRole = AccessibleRole.RadioButton };
                chip.Click += (o, e) => Pick(_chips.IndexOf(chip));
                _chips.Add(chip);
                _flow.Controls.Add(chip);
            }
            for (int i = 0; i < _chips.Count; i++)
            {
                _chips[i].Visible = i < items.Count;
                if (i < items.Count) _chips[i].Text = items[i];
            }
            UpdateChips();
        }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                _selected = value;
                UpdateChips();
            }
        }

        void UpdateChips()
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                _chips[i].Checked = i == _selected;
                _chips[i].TabStop = i == _selected || (_selected < 0 && i == 0);
            }
        }

        void Pick(int index)
        {
            if (index < 0) return;
            SelectedIndex = index;
            EventHandler handler = Picked;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>Eingabe auf einem Knopf waehlt ihn (die Leertaste macht das schon von sich aus).</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter)
            {
                for (int i = 0; i < _chips.Count; i++)
                {
                    if (!_chips[i].Focused) continue;
                    Pick(i);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
