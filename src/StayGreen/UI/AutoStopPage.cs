using System;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Feierabend: automatisch beenden, taeglich, einmalig oder am Ende des Zeitplans, optional mit Teams beenden, Sperren,
    /// Herunterfahren. Vor den stoerenden Aktionen warnt ein Countdown, den man abbrechen oder verschieben kann.
    /// </summary>
    sealed class AutoStopPage : PageBase
    {
        readonly Card _card = new Card();

        readonly SwitchBox _enable = CreateSwitch();
        readonly Segmented _when = new Segmented();
        readonly DateTimePicker _dailyTime = CreateTimePicker();
        readonly DateTimePicker _onceDate = CreateDatePicker();
        readonly DateTimePicker _onceTime = CreateTimePicker();
        readonly SwitchBox _teams = CreateSwitch();
        readonly SwitchBox _lock = CreateSwitch();
        readonly SwitchBox _shutdown = CreateSwitch();
        readonly SwitchBox _exit = CreateSwitch();
        readonly NumericUpDown _warn = Style(new NumericUpDown { Minimum = Settings.MinWarnSeconds, Maximum = Settings.MaxWarnSeconds });
        readonly NumericUpDown _snooze = Style(new NumericUpDown { Minimum = Settings.MinSnoozeMinutes, Maximum = Settings.MaxSnoozeMinutes });
        readonly Label _warnUnit = CreateLabel();
        readonly Label _snoozeUnit = CreateLabel();
        readonly WrapLabel _actionsHeader = new WrapLabel { ForeColor = Theme.MutedStrong };
        readonly WrapLabel _scheduleHint = CreateHint();
        readonly WrapLabel _info = CreateHint();

        readonly SettingRow _enableRow;
        readonly SettingRow _whenRow;
        readonly SettingRow _timeRow;
        readonly SettingRow _dateRow;
        readonly SettingRow _teamsRow;
        readonly SettingRow _lockRow;
        readonly SettingRow _shutdownRow;
        readonly SettingRow _exitRow;
        readonly SettingRow _warnRow;
        readonly SettingRow _snoozeRow;
        readonly Stack _whenBarRow;
        readonly Stack _actionsHeaderRow;
        readonly Stack _scheduleHintRow;
        readonly Stack _infoRow;

        public AutoStopPage()
        {
            _warn.Width = Dpi.Px(76);
            _snooze.Width = Dpi.Px(76);

            _enableRow = new SettingRow(_enable);
            _whenRow = new SettingRow(null);          // nur die Beschriftung; die Leiste ist zu breit fuer die rechte Seite
            _whenBarRow = Pad(_when, 0, 10);
            _timeRow = new SettingRow(new InlineRow(_dailyTime));
            _dateRow = new SettingRow(new InlineRow(_onceDate, _onceTime));
            _teamsRow = new SettingRow(_teams);
            _lockRow = new SettingRow(_lock);
            _shutdownRow = new SettingRow(_shutdown);
            _exitRow = new SettingRow(_exit);
            _warnRow = new SettingRow(new InlineRow(_warn, _warnUnit));
            _snoozeRow = new SettingRow(new InlineRow(_snooze, _snoozeUnit));
            _actionsHeaderRow = Pad(_actionsHeader, 12, 0);
            _scheduleHintRow = Pad(_scheduleHint, 2, 4);
            _infoRow = Pad(_info, 4, 2);

            _card.AddRow(_enableRow);
            _card.AddRow(_whenRow);
            _card.Add(_whenBarRow);
            _card.AddRow(_timeRow);
            _card.AddRow(_dateRow);
            _card.Add(_scheduleHintRow);
            _card.AddRow(_actionsHeaderRow);
            _card.AddRow(_teamsRow);
            _card.AddRow(_lockRow);
            _card.AddRow(_shutdownRow);
            _card.AddRow(_exitRow);
            _card.AddRow(_warnRow);
            _card.AddRow(_snoozeRow);
            _card.Add(_infoRow);
            Controls.Add(_card);

            _enable.CheckedChanged += (o, e) =>
            {
                UpdateVisibility();
                if (Loading || S == null) return;
                S.AutoStopEnabled = _enable.Checked;
                Fire();
            };
            _when.SelectedIndexChanged += (o, e) =>
            {
                UpdateVisibility();
                if (Loading || S == null) return;
                S.AutoStopTiming = (StopTiming)_when.SelectedIndex;
                if (S.AutoStopTiming == StopTiming.Once) S.AutoStopOnce = OnceValue();
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
            _teams.CheckedChanged += (o, e) => { UpdateVisibility(); if (!Loading && S != null) { S.StopCloseTeams = _teams.Checked; Fire(); } };
            _lock.CheckedChanged += (o, e) => { UpdateVisibility(); if (!Loading && S != null) { S.StopLock = _lock.Checked; Fire(); } };
            _shutdown.CheckedChanged += (o, e) => { UpdateVisibility(); if (!Loading && S != null) { S.StopShutdown = _shutdown.Checked; Fire(); } };
            _exit.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StopExitApp = _exit.Checked; Fire(); } };
            _warn.ValueChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.AutoStopWarnSeconds = (int)_warn.Value;
                Fire();
            };
            _snooze.ValueChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.AutoStopSnoozeMinutes = (int)_snooze.Value;
                Fire();
            };
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
            _when.SelectedIndex = (int)s.AutoStopTiming;
            _dailyTime.Value = DateTime.Today + s.AutoStopTime;
            DateTime once = s.AutoStopOnce == DateTime.MinValue ? SuggestOnce() : s.AutoStopOnce;
            once = once < _onceDate.MinDate ? _onceDate.MinDate : (once > _onceDate.MaxDate ? _onceDate.MaxDate : once);
            _onceDate.Value = once;
            _onceTime.Value = once;
            _teams.Checked = s.StopCloseTeams;
            _lock.Checked = s.StopLock;
            _shutdown.Checked = s.StopShutdown;
            _exit.Checked = s.StopExitApp;
            _warn.Value = Math.Min(Math.Max(s.AutoStopWarnSeconds, Settings.MinWarnSeconds), Settings.MaxWarnSeconds);
            _snooze.Value = Math.Min(Math.Max(s.AutoStopSnoozeMinutes, Settings.MinSnoozeMinutes), Settings.MaxSnoozeMinutes);
            UpdateVisibility();
        }

        /// <summary>Die Einzelheiten sind nur sichtbar, solange der Auto-Stopp eingeschaltet ist.</summary>
        void UpdateVisibility()
        {
            bool on = _enable.Checked;
            var timing = (StopTiming)Math.Max(0, _when.SelectedIndex);
            bool needsWarning = _teams.Checked || _lock.Checked || _shutdown.Checked;
            _whenRow.Visible = on;
            _whenBarRow.Visible = on;
            _timeRow.Visible = on && timing == StopTiming.Daily;
            _dateRow.Visible = on && timing == StopTiming.Once;
            _scheduleHintRow.Visible = on && timing == StopTiming.ScheduleEnd;
            _actionsHeaderRow.Visible = on;
            _teamsRow.Visible = on;
            _lockRow.Visible = on;
            _shutdownRow.Visible = on;
            _exitRow.Visible = on;
            _warnRow.Visible = on && needsWarning;
            _snoozeRow.Visible = on && needsWarning;
            _infoRow.Visible = on && _info.Text.Length > 0;
            RefreshLayout();
        }

        public override void ApplyTexts()
        {
            _card.Text = Loc.T("stop.grp.title");
            _enableRow.Title = Loc.T("stop.enable");
            _enableRow.Caption = Loc.T("stop.intro");

            _when.Items = new[] { Loc.T("stop.mode.daily"), Loc.T("stop.mode.once"), Loc.T("stop.mode.schedule") };
            _whenRow.Title = Loc.T("stop.when");
            _when.AccessibleName = Loc.T("stop.when");
            _timeRow.Title = Loc.T("stop.time");
            _dateRow.Title = Loc.T("stop.datetime");
            _scheduleHint.Text = Loc.T("stop.schedule.hint");
            _dailyTime.AccessibleName = Loc.T("stop.time");
            _onceDate.AccessibleName = Loc.T("stop.datetime") + " (" + Loc.T("sch.from") + ")";
            _onceTime.AccessibleName = Loc.T("stop.datetime") + " (" + Loc.T("stop.time") + ")";

            _actionsHeader.Text = Loc.T("stop.grp.actions");
            _teamsRow.Title = Loc.T("stop.teams");
            _teamsRow.Caption = Loc.T("stop.teams.hint");
            _lockRow.Title = Loc.T("stop.lock");
            _shutdownRow.Title = Loc.T("stop.shutdown");
            _shutdownRow.Caption = Loc.T("stop.shutdown.hint");
            _exitRow.Title = Loc.T("stop.exit");
            _exitRow.Caption = Loc.T("stop.exit.hint");
            _warnRow.Title = Loc.T("stop.warn");
            _warnRow.Caption = Loc.T("stop.warn.hint");
            _warnUnit.Text = Loc.T("stop.warn.unit");
            _snoozeRow.Title = Loc.T("stop.snooze");
            _snoozeUnit.Text = Loc.T("stop.snooze.unit");
            RefreshLayout();
        }

        public override void Tick(DateTime now, DateTime? autoStopDue)
        {
            if (S == null) return;
            string line = StatusBuilder.AutoStopLine(S, now, autoStopDue) ?? "";
            if (_info.Text == line) return;
            _info.Text = line;
            UpdateVisibility();
        }
    }
}
