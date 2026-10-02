using System;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Zeitfenster, in denen aktiv gehalten wird (z. B. Mo-Fr 08:00-17:00), und Ausnahmetage (Urlaub, Feiertage),
    /// an denen kein Fenster beginnt. Liegt auf der Seite "Zeitplan".
    /// </summary>
    sealed class SchedulePage : PageBase
    {
        readonly Card _card = new Card();
        readonly Card _editCard = new Card();
        readonly Card _exCard = new Card();

        readonly SwitchBox _enable = CreateSwitch();
        readonly SettingRow _enableRow;
        readonly ListBox _list = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            Dock = DockStyle.Fill,
            BackColor = Theme.Card,
        };
        readonly Frame _frame = new Frame();
        readonly Stack _listRow;
        readonly WrapLabel _info = CreateHint();

        readonly ChipBox[] _days = new ChipBox[7];
        readonly Label _fromLabel = CreateLabel();
        readonly Label _toLabel = CreateLabel();
        readonly DateTimePicker _from = CreateTimePicker();
        readonly DateTimePicker _to = CreateTimePicker();
        readonly FlatButton _add = CreateButton(ButtonKind.Primary);
        readonly FlatButton _update = CreateButton(ButtonKind.Secondary);
        readonly FlatButton _remove = CreateButton(ButtonKind.Secondary);
        readonly FlatButton _preset = CreateButton(ButtonKind.Secondary);
        readonly WrapLabel _daysLabel = new WrapLabel { ForeColor = Theme.Text };
        readonly WrapLabel _midnightHint = CreateHint();
        readonly WrapLabel _error = new WrapLabel { ForeColor = Theme.Red };

        // Ausnahmetage
        readonly ListBox _exList = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            Dock = DockStyle.Fill,
            BackColor = Theme.Card,
        };
        readonly Frame _exFrame = new Frame();
        readonly Stack _exListRow;
        readonly Stack _exNoneRow;
        readonly WrapLabel _exHint = CreateHint();
        readonly WrapLabel _exNone = CreateHint();
        readonly Label _exFromLabel = CreateLabel();
        readonly Label _exToLabel = CreateLabel();
        readonly DateTimePicker _exFrom = CreateDatePicker();
        readonly DateTimePicker _exTo = CreateDatePicker();
        readonly FlatButton _exAdd = CreateButton(ButtonKind.Primary);
        readonly FlatButton _exRemove = CreateButton(ButtonKind.Secondary);
        readonly WrapLabel _exError = new WrapLabel { ForeColor = Theme.Red };

        public SchedulePage()
        {
            _enableRow = new SettingRow(_enable);
            _card.AddRow(_enableRow);

            _frame.Height = Dpi.Px(128);
            _frame.Controls.Add(_list);
            _list.ItemHeight = Dpi.Px(32);
            _list.DrawItem += DrawListItem;
            _listRow = Pad(_frame, 4, 6);
            _card.AddRow(_listRow);
            _card.Add(Pad(_info, 0, 2));

            var dayRow = new EvenRow { Inset = new Padding(0, 2, 0, 6) };
            for (int i = 0; i < 7; i++)
            {
                _days[i] = new ChipBox();
                dayRow.Controls.Add(_days[i]);
            }
            var timeRow = new InlineRow(_fromLabel, _from, _toLabel, _to) { Inset = new Padding(0, 4, 0, 4) };
            var buttonRow = new InlineRow(_add, _update, _remove) { Inset = new Padding(0, 6, 0, 2) };
            var presetRow = new InlineRow(_preset) { Inset = new Padding(0, 6, 0, 2) };

            _editCard.Add(_daysLabel);
            _editCard.Add(dayRow);
            _editCard.Add(timeRow);
            _editCard.Add(buttonRow);
            _editCard.Add(presetRow);
            _editCard.Add(Pad(_midnightHint, 4, 0));
            _editCard.Add(_error);

            // Ausnahmetage: Liste, darunter von/bis und die Knoepfe.
            _exFrame.Height = Dpi.Px(96);
            _exFrame.Controls.Add(_exList);
            _exList.ItemHeight = Dpi.Px(32);
            _exList.DrawItem += DrawListItem;
            _exListRow = Pad(_exFrame, 4, 6);
            _exNoneRow = Pad(_exNone, 0, 4);
            var exDateRow = new InlineRow(_exFromLabel, _exFrom, _exToLabel, _exTo) { Inset = new Padding(0, 4, 0, 4) };
            var exButtonRow = new InlineRow(_exAdd, _exRemove) { Inset = new Padding(0, 6, 0, 2) };
            _exCard.Add(Pad(_exHint, 0, 4));
            _exCard.Add(_exListRow);
            _exCard.Add(_exNoneRow);
            _exCard.Add(exDateRow);
            _exCard.Add(exButtonRow);
            _exCard.Add(_exError);

            Controls.Add(_card);
            Controls.Add(_editCard);
            Controls.Add(_exCard);

            _enable.CheckedChanged += (o, e) =>
            {
                UpdateVisibility();
                if (Loading || S == null) return;
                S.ScheduleEnabled = _enable.Checked;
                Fire();
            };
            _list.SelectedIndexChanged += (o, e) =>
            {
                int index = _list.SelectedIndex;
                if (index >= 0 && S != null && index < S.Rules.Count) LoadEditor(S.Rules[index]);
                UpdateButtons();
            };
            _add.Click += (o, e) =>
            {
                ScheduleRule rule = ReadEditor();
                if (rule == null) return;
                S.Rules.Add(rule);
                RefreshList(S.Rules.Count - 1);
                Fire();
            };
            _update.Click += (o, e) =>
            {
                int index = _list.SelectedIndex;
                if (index < 0 || index >= S.Rules.Count) return;
                ScheduleRule rule = ReadEditor();
                if (rule == null) return;
                S.Rules[index] = rule;
                RefreshList(index);
                Fire();
            };
            _remove.Click += (o, e) =>
            {
                int index = _list.SelectedIndex;
                if (index < 0 || index >= S.Rules.Count) return;
                S.Rules.RemoveAt(index);
                RefreshList(Math.Min(index, S.Rules.Count - 1));
                Fire();
            };
            _preset.Click += (o, e) =>
            {
                S.Rules.Add(ScheduleRule.Weekdays(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)));
                SetError("");
                RefreshList(S.Rules.Count - 1);
                Fire();
            };

            _exList.SelectedIndexChanged += (o, e) => UpdateExceptionButtons();
            _exAdd.Click += (o, e) =>
            {
                DateTime from = _exFrom.Value.Date;
                DateTime to = _exTo.Value.Date;
                if (to < from)
                {
                    SetExceptionError(Loc.T("exc.err.order"));
                    return;
                }
                SetExceptionError("");
                S.Exceptions.Add(new DateRange(from, to));
                DateRange.Normalize(S.Exceptions);
                RefreshExceptions(-1);
                Fire();
            };
            _exRemove.Click += (o, e) =>
            {
                int index = _exList.SelectedIndex;
                if (index < 0 || index >= S.Exceptions.Count) return;
                S.Exceptions.RemoveAt(index);
                RefreshExceptions(Math.Min(index, S.Exceptions.Count - 1));
                Fire();
            };
        }

        /// <summary>Zeichnet einen Listeneintrag (Zeitfenster oder Ausnahme) im Stil der Karten.</summary>
        void DrawListItem(object sender, DrawItemEventArgs e)
        {
            var list = (ListBox)sender;
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Rectangle r = e.Bounds;
            using (var back = new SolidBrush(selected ? Theme.TintFor(StatusKind.Active) : Theme.Card))
                e.Graphics.FillRectangle(back, r);
            using (var pen = new Pen(Theme.Divider))
                e.Graphics.DrawLine(pen, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
            var text = new Rectangle(r.Left + Dpi.Px(10), r.Top, r.Width - Dpi.Px(20), r.Height);
            TextRenderer.DrawText(e.Graphics, (string)list.Items[e.Index], e.Font, text, Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _enable.Checked = s.ScheduleEnabled;
            RefreshList(s.Rules.Count > 0 ? 0 : -1);
            if (s.Rules.Count == 0) LoadEditor(ScheduleRule.Weekdays(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)));
            Quiet(() =>
            {
                _exFrom.Value = DateTime.Today;
                _exTo.Value = DateTime.Today;
            });
            RefreshExceptions(-1);
            UpdateVisibility();
        }

        /// <summary>Listen und Editoren sind nur sichtbar, solange der Zeitplan eingeschaltet ist.</summary>
        void UpdateVisibility()
        {
            bool on = _enable.Checked;
            bool hasExceptions = S != null && S.Exceptions.Count > 0;
            _listRow.Visible = on;
            _info.Parent.Visible = on && _info.Text.Length > 0;
            _editCard.Visible = on;
            _exCard.Visible = on;
            _exListRow.Visible = on && hasExceptions;
            _exNoneRow.Visible = on && !hasExceptions;
            RefreshLayout();
        }

        void RefreshList(int select)
        {
            Quiet(() =>
            {
                _list.Items.Clear();
                if (S != null)
                    foreach (ScheduleRule rule in S.Rules)
                        _list.Items.Add(RuleFormatter.Describe(rule));
                if (select >= 0 && select < _list.Items.Count) _list.SelectedIndex = select;
            });
            if (_list.SelectedIndex >= 0 && S != null) LoadEditor(S.Rules[_list.SelectedIndex]);
            UpdateButtons();
        }

        void RefreshExceptions(int select)
        {
            Quiet(() =>
            {
                _exList.Items.Clear();
                if (S != null)
                    foreach (DateRange range in S.Exceptions)
                        _exList.Items.Add(RuleFormatter.Describe(range));
                if (select >= 0 && select < _exList.Items.Count) _exList.SelectedIndex = select;
            });
            UpdateExceptionButtons();
            UpdateVisibility();
        }

        void UpdateButtons()
        {
            bool selected = _list.SelectedIndex >= 0;
            _update.Enabled = selected;
            _remove.Enabled = selected;
        }

        void UpdateExceptionButtons()
        {
            _exRemove.Enabled = _exList.SelectedIndex >= 0;
        }

        void LoadEditor(ScheduleRule rule)
        {
            Quiet(() =>
            {
                for (int i = 0; i < 7; i++) _days[i].Checked = rule.Days[i];
                _from.Value = DateTime.Today + rule.Start;
                _to.Value = DateTime.Today + rule.End;
            });
            SetError("");
        }

        ScheduleRule ReadEditor()
        {
            var rule = new ScheduleRule();
            for (int i = 0; i < 7; i++) rule.Days[i] = _days[i].Checked;
            rule.Start = _from.Value.TimeOfDay;
            rule.End = _to.Value.TimeOfDay;
            rule.Start = new TimeSpan(rule.Start.Hours, rule.Start.Minutes, 0);
            rule.End = new TimeSpan(rule.End.Hours, rule.End.Minutes, 0);

            if (!rule.AnyDay)
            {
                SetError(Loc.T("sch.err.nodays"));
                return null;
            }
            if (rule.Start == rule.End)
            {
                SetError(Loc.T("sch.err.sametime"));
                return null;
            }
            SetError("");
            return rule;
        }

        void SetError(string text)
        {
            _error.Text = text;
            _error.Visible = text.Length > 0;
            RefreshLayout();
        }

        void SetExceptionError(string text)
        {
            _exError.Text = text;
            _exError.Visible = text.Length > 0;
            RefreshLayout();
        }

        public override void ApplyTexts()
        {
            _card.Text = Loc.T("sch.grp.title");
            _enableRow.Title = Loc.T("sch.enable");
            _enableRow.Caption = Loc.T("sch.hint");

            _editCard.Text = Loc.T("sch.grp.edit");
            _daysLabel.Text = Loc.T("sch.days");
            for (int i = 0; i < 7; i++)
            {
                _days[i].Text = Loc.DayShort(i);
                _days[i].AccessibleName = DayName(i);      // Screenreader lesen "Montag" statt "Mo"
            }
            _fromLabel.Text = Loc.T("sch.from");
            _toLabel.Text = Loc.T("sch.to");
            _from.AccessibleName = Loc.T("sch.grp.edit") + ": " + Loc.T("sch.from");
            _to.AccessibleName = Loc.T("sch.grp.edit") + ": " + Loc.T("sch.to");
            _list.AccessibleName = Loc.T("sch.grp.title");
            _add.Text = Loc.T("sch.add");
            _update.Text = Loc.T("sch.update");
            _remove.Text = Loc.T("sch.remove");
            _preset.Text = Loc.T("sch.preset");
            _midnightHint.Text = Loc.T("sch.hint.midnight");
            _error.Visible = _error.Text.Length > 0;

            _exCard.Text = Loc.T("exc.grp");
            _exHint.Text = Loc.T("exc.hint");
            _exNone.Text = Loc.T("exc.none");
            _exFromLabel.Text = Loc.T("sch.from");
            _exToLabel.Text = Loc.T("sch.to");
            _exFrom.AccessibleName = Loc.T("exc.grp") + ": " + Loc.T("sch.from");
            _exTo.AccessibleName = Loc.T("exc.grp") + ": " + Loc.T("sch.to");
            _exList.AccessibleName = Loc.T("exc.grp");
            _exAdd.Text = Loc.T("sch.add");
            _exRemove.Text = Loc.T("sch.remove");
            _exError.Visible = _exError.Text.Length > 0;

            if (S != null)
            {
                int selected = _list.SelectedIndex;
                RefreshList(selected);
                RefreshExceptions(_exList.SelectedIndex);
            }
            RefreshLayout();
        }

        public override void Tick(DateTime now, DateTime? autoStopDue)
        {
            if (S == null) return;
            string line = S.Rules.Count == 0 && S.ScheduleEnabled
                ? Loc.T("sch.none")
                : StatusBuilder.ScheduleLine(S, now);
            line = line ?? "";
            if (_info.Text == line) return;
            _info.Text = line;
            UpdateVisibility();
        }
    }
}
