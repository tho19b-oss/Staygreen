using System;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>Zeitfenster, in denen aktiv gehalten wird (z. B. Mo-Fr 08:00-17:00).</summary>
    sealed class SchedulePage : PageBase
    {
        readonly CheckBox _enable = CreateCheck();
        readonly WrapLabel _hint = CreateHint();
        readonly ListBox _list = new ListBox { IntegralHeight = false, Height = 104 };
        readonly GroupBox _group;
        readonly CheckBox[] _days = new CheckBox[7];
        readonly Label _fromLabel = CreateLabel();
        readonly Label _toLabel = CreateLabel();
        readonly DateTimePicker _from = CreateTimePicker();
        readonly DateTimePicker _to = CreateTimePicker();
        readonly Button _add = CreateButton();
        readonly Button _update = CreateButton();
        readonly Button _remove = CreateButton();
        readonly Button _preset = CreateButton();
        readonly WrapLabel _midnightHint = CreateHint();
        readonly WrapLabel _error = new WrapLabel { ForeColor = Theme.Red };
        readonly WrapLabel _info = new WrapLabel();

        public SchedulePage()
        {
            TableLayoutPanel root = CreateRoot();

            _list.Margin = new Padding(3, 4, 3, 4);

            var dayFlow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Dock = DockStyle.Top };
            for (int i = 0; i < 7; i++)
            {
                _days[i] = CreateCheck();
                _days[i].Margin = new Padding(3, 3, 6, 3);
                dayFlow.Controls.Add(_days[i]);
            }

            var timeFlow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Dock = DockStyle.Top };
            timeFlow.Controls.Add(_fromLabel);
            timeFlow.Controls.Add(_from);
            timeFlow.Controls.Add(_toLabel);
            timeFlow.Controls.Add(_to);
            _from.Margin = new Padding(3, 3, 12, 3);
            _to.Margin = new Padding(3, 3, 3, 3);

            var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Dock = DockStyle.Top };
            buttons.Controls.Add(_add);
            buttons.Controls.Add(_update);
            buttons.Controls.Add(_remove);
            buttons.Controls.Add(_preset);

            var editor = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1 };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            editor.Controls.Add(dayFlow);
            editor.Controls.Add(timeFlow);
            editor.Controls.Add(buttons);
            editor.Controls.Add(_midnightHint);
            _midnightHint.Dock = DockStyle.Fill;
            _group = CreateGroup(editor);

            AddRow(root, _enable);
            AddRow(root, _hint);
            AddRow(root, _list);
            AddRow(root, _group);
            AddRow(root, _error);
            AddRow(root, _info);

            _enable.CheckedChanged += (o, e) =>
            {
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
                _error.Text = "";
                RefreshList(S.Rules.Count - 1);
                Fire();
            };
        }

        static DateTimePicker CreateTimePicker()
        {
            return new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "HH:mm",
                ShowUpDown = true,
                Width = 72,
                Anchor = AnchorStyles.Left,
            };
        }

        static Button CreateButton()
        {
            return new Button { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 1, 8, 1) };
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _enable.Checked = s.ScheduleEnabled;
            RefreshList(s.Rules.Count > 0 ? 0 : -1);
            if (s.Rules.Count == 0) LoadEditor(ScheduleRule.Weekdays(new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)));
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

        void UpdateButtons()
        {
            bool selected = _list.SelectedIndex >= 0;
            _update.Enabled = selected;
            _remove.Enabled = selected;
        }

        void LoadEditor(ScheduleRule rule)
        {
            Quiet(() =>
            {
                for (int i = 0; i < 7; i++) _days[i].Checked = rule.Days[i];
                _from.Value = DateTime.Today + rule.Start;
                _to.Value = DateTime.Today + rule.End;
            });
            _error.Text = "";
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
                _error.Text = Loc.T("sch.err.nodays");
                return null;
            }
            if (rule.Start == rule.End)
            {
                _error.Text = Loc.T("sch.err.sametime");
                return null;
            }
            _error.Text = "";
            return rule;
        }

        public override void ApplyTexts()
        {
            _enable.Text = Loc.T("sch.enable");
            _hint.Text = Loc.T("sch.hint");
            _group.Text = Loc.T("sch.grp.edit");
            for (int i = 0; i < 7; i++) _days[i].Text = Loc.DayShort(i);
            _fromLabel.Text = Loc.T("sch.from");
            _toLabel.Text = Loc.T("sch.to");
            _add.Text = Loc.T("sch.add");
            _update.Text = Loc.T("sch.update");
            _remove.Text = Loc.T("sch.remove");
            _preset.Text = Loc.T("sch.preset");
            _midnightHint.Text = Loc.T("sch.hint.midnight");
            if (S != null)
            {
                int selected = _list.SelectedIndex;
                RefreshList(selected);
            }
            RefreshLayout();
        }

        public override void Tick(DateTime now, DateTime? autoStopDue)
        {
            if (S == null) return;
            string line = S.Rules.Count == 0 && S.ScheduleEnabled
                ? Loc.T("sch.none")
                : StatusBuilder.ScheduleLine(S, now);
            _info.Text = line ?? "";
        }
    }
}
