using System;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>Automatisch beenden: taeglich oder einmalig, optional mit Teams beenden, Sperren, Herunterfahren.</summary>
    sealed class AutoStopPage : PageBase
    {
        readonly CheckBox _enable = CreateCheck();
        readonly WrapLabel _intro = CreateHint();
        readonly RadioButton _daily = new RadioButton { AutoSize = true, UseMnemonic = false };
        readonly RadioButton _once = new RadioButton { AutoSize = true, UseMnemonic = false };
        readonly DateTimePicker _dailyTime = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 72,
            Anchor = AnchorStyles.Left,
        };
        // Kurzes Datum nach den Windows-Regionseinstellungen (nicht fest deutsch, auch wenn die Oberflaeche englisch ist).
        readonly DateTimePicker _onceDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Width = 120,
        };
        readonly DateTimePicker _onceTime = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 72,
        };
        readonly GroupBox _group;
        readonly CheckBox _teams = CreateCheck();
        readonly CheckBox _lock = CreateCheck();
        readonly CheckBox _shutdown = CreateCheck();
        readonly CheckBox _exit = CreateCheck();
        readonly WrapLabel _shutdownHint = CreateHint();
        readonly WrapLabel _info = new WrapLabel();
        readonly TableLayoutPanel _timing;

        public AutoStopPage()
        {
            TableLayoutPanel root = CreateRoot();

            _timing = CreateGrid(2);
            _timing.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _timing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _daily.Anchor = AnchorStyles.Left;
            _once.Anchor = AnchorStyles.Left;
            _daily.Margin = new Padding(20, 4, 12, 4);
            _once.Margin = new Padding(20, 4, 12, 4);
            _timing.Controls.Add(_daily, 0, 0);
            _timing.Controls.Add(_dailyTime, 1, 0);
            _timing.Controls.Add(_once, 0, 1);
            var onceFlow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.Left };
            _onceDate.Margin = new Padding(3, 3, 8, 3);
            onceFlow.Controls.Add(_onceDate);
            onceFlow.Controls.Add(_onceTime);
            _timing.Controls.Add(onceFlow, 1, 1);

            var actions = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1 };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            foreach (CheckBox box in new[] { _teams, _lock, _shutdown })
            {
                box.Margin = new Padding(3, 3, 3, 3);
                actions.Controls.Add(box);
            }
            actions.Controls.Add(_shutdownHint);
            _shutdownHint.Margin = new Padding(22, 0, 3, 6);
            _shutdownHint.Dock = DockStyle.Fill;
            _exit.Margin = new Padding(3, 3, 3, 3);
            actions.Controls.Add(_exit);
            _group = CreateGroup(actions);

            AddRow(root, _enable);
            AddRow(root, _intro);
            AddRow(root, _timing);
            AddRow(root, _group);
            AddRow(root, _info);

            _enable.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.AutoStopEnabled = _enable.Checked;
                UpdateEnabled();
                Fire();
            };
            _daily.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null || !_daily.Checked) return;
                S.AutoStopTiming = StopTiming.Daily;
                UpdateEnabled();
                Fire();
            };
            _once.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null || !_once.Checked) return;
                S.AutoStopTiming = StopTiming.Once;
                S.AutoStopOnce = OnceValue();
                UpdateEnabled();
                Fire();
            };
            _dailyTime.ValueChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                TimeSpan t = _dailyTime.Value.TimeOfDay;
                S.AutoStopTime = new TimeSpan(t.Hours, t.Minutes, 0);
                Fire();
            };
            EventHandler onceChanged = (o, e) =>
            {
                if (Loading || S == null) return;
                S.AutoStopOnce = OnceValue();
                Fire();
            };
            _onceDate.ValueChanged += onceChanged;
            _onceTime.ValueChanged += onceChanged;
            _teams.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StopCloseTeams = _teams.Checked; Fire(); } };
            _lock.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StopLock = _lock.Checked; Fire(); } };
            _shutdown.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StopShutdown = _shutdown.Checked; Fire(); } };
            _exit.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StopExitApp = _exit.Checked; Fire(); } };
        }

        DateTime OnceValue()
        {
            DateTime time = _onceTime.Value;
            return _onceDate.Value.Date.AddHours(time.Hour).AddMinutes(time.Minute);
        }

        /// <summary>Sinnvoller Vorschlag fuer "einmalig": heute 17:00, sonst morgen 17:00.</summary>
        static DateTime SuggestOnce()
        {
            DateTime t = DateTime.Today.AddHours(17);
            return t > DateTime.Now ? t : t.AddDays(1);
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _enable.Checked = s.AutoStopEnabled;
            _daily.Checked = s.AutoStopTiming == StopTiming.Daily;
            _once.Checked = s.AutoStopTiming == StopTiming.Once;
            _dailyTime.Value = DateTime.Today + s.AutoStopTime;
            DateTime once = s.AutoStopOnce == DateTime.MinValue ? SuggestOnce() : s.AutoStopOnce;
            once = once < _onceDate.MinDate ? _onceDate.MinDate : (once > _onceDate.MaxDate ? _onceDate.MaxDate : once);
            _onceDate.Value = once;
            _onceTime.Value = once;
            _teams.Checked = s.StopCloseTeams;
            _lock.Checked = s.StopLock;
            _shutdown.Checked = s.StopShutdown;
            _exit.Checked = s.StopExitApp;
            UpdateEnabled();
        }

        void UpdateEnabled()
        {
            bool on = _enable.Checked;
            _timing.Enabled = on;
            _group.Enabled = on;
            _dailyTime.Enabled = _daily.Checked;
            _onceDate.Enabled = _once.Checked;
            _onceTime.Enabled = _once.Checked;
        }

        public override void ApplyTexts()
        {
            _enable.Text = Loc.T("stop.enable");
            _intro.Text = Loc.T("stop.intro");
            _daily.Text = Loc.T("stop.daily");
            _once.Text = Loc.T("stop.once");
            _group.Text = Loc.T("stop.grp.actions");
            _teams.Text = Loc.T("stop.teams");
            _lock.Text = Loc.T("stop.lock");
            _shutdown.Text = Loc.T("stop.shutdown");
            _shutdownHint.Text = Loc.T("stop.shutdown.hint");
            _exit.Text = Loc.T("stop.exit");
            RefreshLayout();
        }

        public override void Tick(DateTime now, DateTime? autoStopDue)
        {
            if (S == null) return;
            _info.Text = StatusBuilder.AutoStopLine(S, now, autoStopDue) ?? "";
        }
    }
}
