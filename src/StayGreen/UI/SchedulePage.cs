using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Karte "Aktive Zeiten" auf der Seite "Zeitplan": oben Titel und Schalter, darunter die Zeitfenster als kompakte Zeilen
    /// (z. B. "Mo-Fr  08:00 - 17:00"). Ein Klick auf eine Zeile klappt an ihrer Stelle den Editor auf (Wochentage, von/bis,
    /// Speichern/Abbrechen/Entfernen), der Muelleimer daneben entfernt sie. Neue Fenster kommen ueber "Zeitfenster
    /// hinzufuegen", bei leerer Liste auch direkt als Buerozeiten.
    /// </summary>
    sealed class SchedulePage : PageBase
    {
        /// <summary>Wert von <see cref="_editing"/>: kein Editor offen.</summary>
        const int NotEditing = -1;

        /// <summary>Wert von <see cref="_editing"/>: Der Editor legt ein neues Zeitfenster an.</summary>
        const int NewRule = -2;

        static readonly TimeSpan OfficeStart = new TimeSpan(8, 0, 0);
        static readonly TimeSpan OfficeEnd = new TimeSpan(17, 0, 0);

        readonly Card _card = new Card();
        readonly SwitchBox _enable = CreateSwitch();
        readonly SettingRow _header;
        readonly WrapLabel _offText = CreateHint();
        readonly Stack _offRow;

        // Eine Zeile je Zeitfenster (die Zeilen werden wiederverwendet), dazu der Editor an der bearbeiteten Stelle. Ein Pixel
        // Abstand oben, damit die erste Zeile die Trennlinie nicht ueberdeckt.
        readonly Stack _list = new Stack { TopDivider = true, Inset = new Padding(0, 1, 0, 0) };
        readonly List<RuleRow> _rows = new List<RuleRow>();

        readonly WrapLabel _emptyText = CreateHint();
        readonly FlatButton _addFirst = CreateButton(ButtonKind.Primary);
        readonly FlatButton _preset = CreateButton(ButtonKind.Secondary);
        readonly Stack _emptyRow = new Stack { Inset = new Padding(0, 12, 0, 2), Gap = 10, TopDivider = true };

        readonly FlatButton _add = CreateButton(ButtonKind.Secondary);
        readonly FlowRow _addRow;

        readonly Stack _editorRow = new Stack { Inset = new Padding(0, 10, 0, 10), BottomDivider = true };
        readonly TintBox _editor = new TintBox { Gap = 10 };
        readonly WrapLabel _editTitle = new WrapLabel { ForeColor = Theme.Text };
        readonly EvenRow _dayRow = new EvenRow();
        readonly ChipBox[] _days = new ChipBox[7];
        readonly Label _fromLabel = CreateLabel();
        readonly Label _toLabel = CreateLabel();
        readonly TimeBox _from = new TimeBox();
        readonly TimeBox _to = new TimeBox();
        readonly Pill _nextDay = new Pill();
        readonly WrapLabel _problem = new WrapLabel { ForeColor = Theme.WarnText };
        readonly FlatButton _save = CreateButton(ButtonKind.Primary);
        readonly FlatButton _cancel = CreateButton(ButtonKind.Secondary);
        readonly FlatButton _delete = CreateButton(ButtonKind.Danger);
        readonly ToolTip _tips = new ToolTip();

        /// <summary>Index des bearbeiteten Zeitfensters, sonst <see cref="NotEditing"/> oder <see cref="NewRule"/>.</summary>
        int _editing = NotEditing;

        /// <summary>Der Editor wird gerade vorbelegt: die Felder melden ihre Aenderungen dann nicht einzeln.</summary>
        bool _filling;

        public SchedulePage()
        {
            // Kopf der Karte: Titel, Erklaerung und Schalter in einer Zeile statt Ueberschrift plus eigener Schalterzeile.
            _card.Text = "";
            _header = new SettingRow(_enable) { Heading = true, Inset = new Padding(0, 0, 0, 12) };
            _card.AddRow(_header);
            _offRow = Pad(_offText, 0, 2);
            _card.Add(_offRow);
            _card.Add(_list);

            _addFirst.Icon = IconKind.Plus;
            _emptyRow.Controls.Add(_emptyText);
            _emptyRow.Controls.Add(new FlowRow(_addFirst, _preset));
            _card.Add(_emptyRow);

            _add.Icon = IconKind.Plus;
            _addRow = new FlowRow(_add) { Inset = new Padding(0, 10, 0, 2) };
            _card.Add(_addRow);

            for (int i = 0; i < 7; i++)
            {
                _days[i] = new ChipBox();
                _dayRow.Controls.Add(_days[i]);
            }
            _dayRow.AccessibleRole = AccessibleRole.Grouping;
            _fromLabel.ForeColor = Theme.MutedStrong;
            _toLabel.ForeColor = Theme.MutedStrong;
            _editor.AccessibleRole = AccessibleRole.Grouping;
            _editor.Controls.Add(_editTitle);
            _editor.Controls.Add(_dayRow);
            _editor.Controls.Add(new FlowRow(_fromLabel, _from, _toLabel, _to, _nextDay));
            _editor.Controls.Add(_problem);
            _editor.Controls.Add(new FlowRow(_save, _cancel, _delete) { PushLastRight = true });
            _editorRow.Controls.Add(_editor);
            _list.Controls.Add(_editorRow);
            UpdateFonts();

            Controls.Add(_card);

            _enable.CheckedChanged += (o, e) =>
            {
                if (!_enable.Checked) _editing = NotEditing;   // Ausschalten verwirft einen offenen Entwurf
                Sync(null);
                if (Loading || S == null) return;
                S.ScheduleEnabled = _enable.Checked;
                Fire();
            };
            _add.Click += (o, e) => OpenEditor(NewRule);
            _addFirst.Click += (o, e) => OpenEditor(NewRule);
            _preset.Click += (o, e) => AddOfficeHours();
            _save.Click += (o, e) => SaveEditor();
            _cancel.Click += (o, e) => CloseEditor();
            _delete.Click += (o, e) => RemoveRule(_editing);
            EventHandler edited = (o, e) =>
            {
                if (!_filling) UpdateEditorState();
            };
            foreach (ChipBox day in _days) day.CheckedChanged += edited;
            _from.ValueChanged += edited;
            _to.ValueChanged += edited;
        }

        int RuleCount
        {
            get { return S == null ? 0 : S.Rules.Count; }
        }

        protected override void Populate(Settings s)
        {
            _editing = NotEditing;   // neu geladene Einstellungen: ein offener Entwurf passt nicht mehr dazu
            ApplyTexts();
            _enable.Checked = s.ScheduleEnabled;
            Sync(null);
        }

        // ------------------------------------------------------------------ Liste

        /// <summary>
        /// Bringt Liste, Editor und Knoepfe auf den Stand der Einstellungen und ordnet neu an. Was sichtbar wird, erscheint
        /// zuerst; dann bekommt <paramref name="focus"/> den Fokus, und erst danach verschwindet der Rest. So springt der
        /// Fokus nie zwischendurch auf ein ganz anderes Element (Windows wuerde die Seite sonst dorthin rollen).
        /// <paramref name="reveal"/> wird danach ganz ins Bild gerollt (ohne Angabe: das Element mit dem Fokus).
        /// </summary>
        void Sync(Control focus, Control reveal = null)
        {
            int count = RuleCount;
            EnsureRows(count);
            if (_editing >= count) _editing = NotEditing;
            bool on = _enable.Checked;

            var show = new List<Control>();
            var hide = new List<Control>();
            Action<Control, bool> want = (c, visible) => (visible ? show : hide).Add(c);

            // Reihenfolge: Zeilen nach Index, der Editor direkt an der bearbeiteten Stelle (ein neues Fenster am Ende).
            int daysWidth = DaysColumnWidth(count);
            var order = new List<Control>();
            for (int i = 0; i < _rows.Count; i++)
            {
                RuleRow row = _rows[i];
                if (i < count) row.SetRule(S.Rules[i], daysWidth);
                want(row, i < count && i != _editing);
                order.Add(row);
                if (i == _editing) order.Add(_editorRow);
            }
            if (_editing == NewRule) order.Insert(count, _editorRow);
            if (!order.Contains(_editorRow)) order.Add(_editorRow);
            for (int i = 0; i < order.Count; i++)
            {
                _list.Controls.SetChildIndex(order[i], i);
                order[i].TabIndex = i;
            }

            want(_editorRow, _editing != NotEditing);
            want(_offRow, !on);
            want(_list, on && (count > 0 || _editing == NewRule));
            want(_emptyRow, on && count == 0 && _editing != NewRule);
            want(_addRow, on && count > 0 && _editing == NotEditing);

            foreach (Control c in show) c.Visible = true;
            if (focus != null) focus.Focus();
            foreach (Control c in hide) c.Visible = false;
            RefreshLayout();
            if (reveal != null || focus != null) ScrollIntoView(reveal ?? focus);
        }

        /// <summary>Legt fehlende Zeilen an; ueberzaehlige bleiben unsichtbar fuer spaeter.</summary>
        void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var row = new RuleRow();
                row.EditButton.Click += (o, e) => OpenEditor(_rows.IndexOf(row));
                row.RemoveButton.Click += (o, e) => RemoveRule(_rows.IndexOf(row));
                _tips.SetToolTip(row.RemoveButton, Loc.T("sch.remove"));
                _rows.Add(row);
                _list.Controls.Add(row);
            }
        }

        /// <summary>Gemeinsame Breite der Tage-Spalte, damit die Uhrzeiten aller Zeilen untereinander stehen.</summary>
        int DaysColumnWidth(int count)
        {
            int width = Dpi.Px(84);
            for (int i = 0; i < count; i++)
                width = Math.Max(width, Metrics.TextWidth(RuleFormatter.DaysLabel(S.Rules[i].Days), Font));
            return width;
        }

        /// <summary>Wohin der Fokus nach einer Aenderung an Zeile <paramref name="index"/> geht: zu ihrem Knopf oder, wird sie gerade bearbeitet, in den Editor.</summary>
        Control RowFocus(int index)
        {
            return index == _editing ? (Control)_days[0] : _rows[index].EditButton;
        }

        /// <summary>Vorlage fuer die leere Liste: Mo-Fr 08:00-17:00 direkt uebernehmen.</summary>
        void AddOfficeHours()
        {
            if (S == null) return;
            bool hadFocus = _preset.Focused;
            S.Rules.Add(ScheduleRule.Weekdays(OfficeStart, OfficeEnd));
            EnsureRows(S.Rules.Count);
            Sync(hadFocus ? RowFocus(S.Rules.Count - 1) : null);
            Fire();
        }

        /// <summary>Entfernt ein Zeitfenster (Muelleimer oder "Entfernen" im Editor); der Fokus geht zur naechsten Zeile.</summary>
        void RemoveRule(int index)
        {
            if (S == null || index < 0 || index >= S.Rules.Count) return;
            bool hadFocus = (index < _rows.Count && _rows[index].ContainsFocus) || _editor.ContainsFocus;
            S.Rules.RemoveAt(index);
            if (_editing == index) _editing = NotEditing;
            else if (_editing > index) _editing--;

            int count = S.Rules.Count;
            Control focus = null;
            if (hadFocus) focus = count == 0 ? _addFirst : RowFocus(Math.Min(index, count - 1));
            Sync(focus);
            Fire();
        }

        // ------------------------------------------------------------------ Editor

        /// <summary>Klappt den Editor fuer ein Zeitfenster auf (<see cref="NewRule"/>: neues Fenster, vorbelegt mit Mo-Fr 08-17).</summary>
        void OpenEditor(int index)
        {
            if (S == null) return;
            ScheduleRule rule;
            if (index == NewRule) rule = ScheduleRule.Weekdays(OfficeStart, OfficeEnd);
            else if (index >= 0 && index < S.Rules.Count) rule = S.Rules[index];
            else return;

            _editing = index;
            _filling = true;
            try
            {
                for (int i = 0; i < 7; i++) _days[i].Checked = rule.Days[i];
                _from.Value = rule.Start;
                _to.Value = rule.End;
            }
            finally
            {
                _filling = false;
            }
            UpdateEditorState();
            Sync(_days[0], _editorRow);
        }

        /// <summary>Schliesst den Editor ohne zu speichern; der Fokus kehrt zur Zeile (bzw. zum Hinzufuegen-Knopf) zurueck.</summary>
        void CloseEditor()
        {
            int index = _editing;
            if (index == NotEditing) return;
            bool hadFocus = _editor.ContainsFocus;
            _editing = NotEditing;

            Control focus = null;
            if (hadFocus) focus = index >= 0 ? _rows[index].EditButton : (RuleCount > 0 ? _add : _addFirst);
            Sync(focus);
        }

        void SaveEditor()
        {
            if (S == null || _editing == NotEditing) return;
            ScheduleRule rule = Draft();
            if (Problem(rule) != null) return;

            int index = _editing;
            if (index == NewRule)
            {
                S.Rules.Add(rule);
                index = S.Rules.Count - 1;
            }
            else if (index < S.Rules.Count)
            {
                S.Rules[index] = rule;
            }
            else
            {
                return;
            }

            bool hadFocus = _editor.ContainsFocus;
            _editing = NotEditing;
            EnsureRows(S.Rules.Count);
            Sync(hadFocus ? _rows[index].EditButton : null);
            Fire();
        }

        ScheduleRule Draft()
        {
            var rule = new ScheduleRule();
            for (int i = 0; i < 7; i++) rule.Days[i] = _days[i].Checked;
            rule.Start = _from.Value;
            rule.End = _to.Value;
            return rule;
        }

        /// <summary>Warum sich ein Entwurf nicht speichern laesst; null, wenn er gueltig ist.</summary>
        static string Problem(ScheduleRule rule)
        {
            if (!rule.AnyDay) return Loc.T("sch.err.nodays");
            if (rule.Start == rule.End) return Loc.T("sch.err.sametime");
            return null;
        }

        /// <summary>Titel, Pruefung und Hinweise des Editors nach jeder Aenderung: Speichern geht nur mit einem gueltigen Fenster.</summary>
        void UpdateEditorState()
        {
            _editTitle.Text = Loc.T(_editing == NewRule ? "sch.grp.new" : "sch.grp.edit");
            _editor.AccessibleName = _editTitle.Text;
            _delete.Visible = _editing >= 0;

            ScheduleRule draft = Draft();
            string problem = Problem(draft) ?? "";
            _problem.Text = problem;
            _problem.Visible = problem.Length > 0;
            _save.Enabled = problem.Length == 0;
            _save.AccessibleDescription = problem.Length > 0 ? problem : null;
            _nextDay.Visible = problem.Length == 0 && RuleFormatter.EndsNextDay(draft);
            RefreshLayout();
        }

        /// <summary>Im Editor: Esc bricht ab, Eingabe speichert (auf einem Knopf loest Eingabe wie gewohnt diesen Knopf aus).</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_editing != NotEditing && _editor.ContainsFocus)
            {
                if (keyData == Keys.Escape)
                {
                    CloseEditor();
                    return true;
                }
                if (keyData == Keys.Enter && !(FocusedIn(_editor) is Button))
                {
                    SaveEditor();
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
                if (c.ContainsFocus) return FocusedIn(c);
            }
            return null;
        }

        // ------------------------------------------------------------------ Texte und Schrift

        public override void ApplyTexts()
        {
            _header.Title = Loc.T("sch.grp.title");
            _header.Caption = Loc.T("sch.hint");
            _enable.AccessibleName = Loc.T("sch.enable");   // nach Title: die Zeile benennt den Schalter sonst nur "Aktive Zeiten"
            _offText.Text = Loc.T("sch.off");
            _emptyText.Text = Loc.T("sch.none");
            _addFirst.Text = Loc.T("sch.add");
            _add.Text = Loc.T("sch.add");
            _preset.Text = Loc.T("sch.preset");

            _dayRow.AccessibleName = Loc.T("sch.days");
            for (int i = 0; i < 7; i++)
            {
                _days[i].Text = Loc.DayShort(i);
                _days[i].AccessibleName = DayName(i);      // Screenreader lesen "Montag" statt "Mo"
            }
            _fromLabel.Text = Loc.T("sch.from");
            _toLabel.Text = Loc.T("sch.to");
            _from.AccessibleName = Loc.T("sch.from");
            _to.AccessibleName = Loc.T("sch.to");
            _nextDay.Text = Loc.T("sch.nextday");
            _save.Text = Loc.T("sch.save");
            _cancel.Text = Loc.T("sch.cancel");
            _delete.Text = Loc.T("sch.remove");
            foreach (RuleRow row in _rows) _tips.SetToolTip(row.RemoveButton, Loc.T("sch.remove"));

            UpdateEditorState();
            Sync(null);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateFonts();
        }

        void UpdateFonts()
        {
            Font old = _editTitle.Font;
            _editTitle.Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
            if (old != null && !ReferenceEquals(old, Font)) old.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Eine Zeile der Liste: links der Zeilen-Knopf (bearbeiten), rechts der Muelleimer (entfernen).</summary>
    sealed class RuleRow : LayoutPanel
    {
        public RuleRow()
        {
            Inset = new Padding(0, 4, 0, 4);
            BottomDivider = true;
            Controls.Add(EditButton);
            Controls.Add(RemoveButton);
        }

        public RuleButton EditButton { get; } = new RuleButton();

        public IconButton RemoveButton { get; } = new IconButton { Icon = IconKind.Trash };

        /// <summary>Zeigt ein Zeitfenster an; <paramref name="daysWidth"/> ist die gemeinsame Breite der Tage-Spalte.</summary>
        public void SetRule(ScheduleRule rule, int daysWidth)
        {
            string spoken = RuleFormatter.Describe(rule);
            string pill = RuleFormatter.EndsNextDay(rule) ? Loc.T("sch.overnight") : "";
            EditButton.SetContent(RuleFormatter.DaysLabel(rule.Days), RuleFormatter.Times(rule), pill, daysWidth);
            EditButton.AccessibleName = Loc.T("sch.row.edit", spoken);
            RemoveButton.AccessibleName = Loc.T("sch.row.remove", spoken);
        }

        public override int MeasureHeight(int width)
        {
            return EditButton.PreferredHeight + Dpi.Px(Inset.Vertical);
        }

        protected override void DoLayout()
        {
            int top = Dpi.Px(Inset.Top);
            int height = Math.Max(1, ClientSize.Height - Dpi.Px(Inset.Vertical));
            Size remove = RemoveButton.Size;
            EditButton.SetBounds(0, top, Math.Max(1, ClientSize.Width - remove.Width - Dpi.Px(4)), height);
            RemoveButton.SetBounds(ClientSize.Width - remove.Width, top + (height - remove.Height) / 2, remove.Width, remove.Height);
        }
    }

    /// <summary>
    /// Der Zeilen-Knopf: Tage, Uhrzeit, bei Bedarf "ueber Nacht" und rechts ein Stift als Hinweis, dass ein Klick (oder
    /// Leertaste/Eingabe) die Zeile zum Bearbeiten oeffnet.
    /// </summary>
    sealed class RuleButton : PaintedButton
    {
        string _days = "";
        string _times = "";
        string _pill = "";
        int _daysWidth;
        Font _timeFont;
        Font _pillFont;

        public RuleButton()
        {
            UpdateFonts();
        }

        /// <summary>Hoehe der Zeile: genug fuer die groessere Schrift der Uhrzeit, mindestens 44 Pixel.</summary>
        public int PreferredHeight
        {
            get { return Math.Max(Dpi.Px(44), _timeFont.Height + Dpi.Px(18)); }
        }

        public void SetContent(string days, string times, string pill, int daysWidth)
        {
            if (_days == days && _times == times && _pill == pill && _daysWidth == daysWidth) return;
            _days = days;
            _times = times;
            _pill = pill;
            _daysWidth = daysWidth;
            Invalidate();
        }

        void UpdateFonts()
        {
            Font oldTime = _timeFont;
            Font oldPill = _pillFont;
            _timeFont = Fonts.Semibold(Font, 1.5f);
            _pillFont = Fonts.Smaller(Font, 0.75f);
            if (oldTime != null) oldTime.Dispose();
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

            int icon = Dpi.Px(IconPainter.Size);
            int right = Width - Dpi.Px(10) - icon;   // hier beginnt der Stift
            int x = Dpi.Px(8);
            int daysWidth = Math.Min(_daysWidth, Math.Max(Dpi.Px(40), (right - x) / 2));
            Draw.LeftText(g, _days, Font, new Rectangle(x, 0, daysWidth, Height), Theme.MutedStrong);
            x += daysWidth + Dpi.Px(12);

            int timesWidth = Math.Min(Metrics.TextWidth(_times, _timeFont), Math.Max(1, right - Dpi.Px(8) - x));
            Draw.LeftText(g, _times, _timeFont, new Rectangle(x, 0, timesWidth, Height), Theme.Text);
            x += timesWidth + Dpi.Px(10);

            if (_pill.Length > 0)
            {
                Size pill = PillPainter.Measure(_pill, _pillFont);
                if (x + pill.Width <= right - Dpi.Px(8))
                    PillPainter.Paint(g, new Rectangle(x, (Height - pill.Height) / 2, pill.Width, pill.Height), _pill, _pillFont);
            }

            IconPainter.Draw(g, IconKind.Pencil, new Rectangle(right, (Height - icon) / 2, icon, icon),
                Hover || Down ? Theme.Text : Theme.Muted);
            if (Focused && ShowFocusCues)
                Draw.FocusRing(g, Rectangle.Inflate(ClientRectangle, -Dpi.Px(2), -Dpi.Px(2)), Dpi.Px(6));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timeFont != null) _timeFont.Dispose();
                if (_pillFont != null) _pillFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
