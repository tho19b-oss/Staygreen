using System;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>Methode, Intervall, intelligenter Modus, Wach-Halten, Teams-Pruefung und Sicherheitsnetz.</summary>
    sealed class ActivityPage : PageBase
    {
        readonly Card _methodCard = new Card();
        readonly Card _behaviorCard = new Card();

        readonly ComboBox _mode = Style(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
        readonly ComboBox _key = Style(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
        readonly NumericUpDown _interval = Style(new NumericUpDown { Minimum = Settings.MinInterval, Maximum = Settings.MaxInterval });
        readonly NumericUpDown _pixels = Style(new NumericUpDown { Minimum = Settings.MinMousePixels, Maximum = Settings.MaxMousePixels });
        readonly NumericUpDown _maxRun = Style(new NumericUpDown { Minimum = 0, Maximum = Settings.MaxRuntimeHoursLimit });
        readonly Label _secondsUnit = CreateLabel();
        readonly Label _pixelsUnit = CreateLabel();
        readonly Label _hoursUnit = CreateLabel();
        readonly SwitchBox _smart = CreateSwitch();
        readonly SwitchBox _awake = CreateSwitch();
        readonly SwitchBox _teamsOnly = CreateSwitch();
        readonly FlatButton _test = CreateButton(ButtonKind.Secondary);

        readonly SettingRow _modeRow;
        readonly SettingRow _keyRow;
        readonly SettingRow _intervalRow;
        readonly SettingRow _pixelsRow;
        readonly SettingRow _testRow;
        readonly SettingRow _smartRow;
        readonly SettingRow _awakeRow;
        readonly SettingRow _teamsRow;
        readonly SettingRow _maxRunRow;

        /// <summary>Wird vom Hauptfenster gesetzt: erzeugt eine Eingabe und meldet, ob Windows sie angenommen und als Aktivitaet gewertet hat.</summary>
        public Func<InputTestResult> TestRequested;

        public ActivityPage()
        {
            _mode.Width = Dpi.Px(230);
            _key.Width = Dpi.Px(230);
            _interval.Width = Dpi.Px(76);
            _pixels.Width = Dpi.Px(76);
            _maxRun.Width = Dpi.Px(76);

            _modeRow = new SettingRow(_mode);
            _keyRow = new SettingRow(_key);
            _intervalRow = new SettingRow(new InlineRow(_interval, _secondsUnit));
            _pixelsRow = new SettingRow(new InlineRow(_pixels, _pixelsUnit));
            _testRow = new SettingRow(_test);
            _smartRow = new SettingRow(_smart);
            _awakeRow = new SettingRow(_awake);
            _teamsRow = new SettingRow(_teamsOnly);
            _maxRunRow = new SettingRow(new InlineRow(_maxRun, _hoursUnit));

            _methodCard.AddRow(_modeRow);
            _methodCard.AddRow(_keyRow);
            _methodCard.AddRow(_intervalRow);
            _methodCard.AddRow(_pixelsRow);
            _methodCard.AddRow(_testRow);
            _behaviorCard.AddRow(_smartRow);
            _behaviorCard.AddRow(_awakeRow);
            _behaviorCard.AddRow(_teamsRow);
            _behaviorCard.AddRow(_maxRunRow);

            Controls.Add(_methodCard);
            Controls.Add(_behaviorCard);

            _mode.SelectedIndexChanged += (o, e) =>
            {
                if (Loading || S == null || _mode.SelectedIndex < 0) return;
                S.Mode = (ActivityMode)_mode.SelectedIndex;
                UpdateEnabled();
                ResetTestResult();
                Fire();
            };
            _key.SelectedIndexChanged += (o, e) =>
            {
                if (Loading || S == null || _key.SelectedIndex < 0) return;
                S.InputKey = (ActivityKey)_key.SelectedIndex;
                ResetTestResult();
                Fire();
            };
            _interval.ValueChanged += (o, e) =>
            {
                UpdateIntervalHint();
                if (Loading || S == null) return;
                S.IntervalSeconds = (int)_interval.Value;
                Fire();
            };
            _pixels.ValueChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.MousePixels = (int)_pixels.Value;
                ResetTestResult();
                Fire();
            };
            _smart.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.SmartIdle = _smart.Checked;
                Fire();
            };
            _awake.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.KeepAwake = _awake.Checked;
                Fire();
            };
            _teamsOnly.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.OnlyWhileTeamsRuns = _teamsOnly.Checked;
                Fire();
            };
            _maxRun.ValueChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.MaxRuntimeHours = (int)_maxRun.Value;
                Fire();
            };
            _test.Click += (o, e) =>
            {
                InputTestResult result = TestRequested != null ? TestRequested() : InputTestResult.Rejected;
                ShowTestResult(result, DateTime.Now);
            };
        }

        /// <summary>
        /// Zeigt das Ergebnis unter der Beschriftung: gruen als Bestaetigung, sonst rot. Die Uhrzeit macht sichtbar,
        /// dass auch ein erneuter Klick wirklich neu getestet hat.
        /// </summary>
        void ShowTestResult(InputTestResult result, DateTime time)
        {
            string key = result == InputTestResult.Confirmed ? "act.test.ok"
                : result == InputTestResult.NotCounted ? "act.test.nocount" : "act.test.fail";
            _testRow.Caption = Loc.T(key, time.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            _testRow.CaptionColor = result == InputTestResult.Confirmed ? Theme.GreenButton : Theme.Red;
            RefreshLayout();
        }

        /// <summary>Ein frueheres Ergebnis gilt nicht mehr, sobald sich Methode, Taste oder Mausweg aendern: zurueck zum Hinweistext.</summary>
        void ResetTestResult()
        {
            _testRow.Caption = Loc.T("act.test.hint");
            _testRow.CaptionColor = Theme.Muted;
            RefreshLayout();
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _mode.SelectedIndex = (int)s.Mode;
            _key.SelectedIndex = (int)s.InputKey;
            _interval.Value = Math.Min(Math.Max(s.IntervalSeconds, Settings.MinInterval), Settings.MaxInterval);
            _pixels.Value = Math.Min(Math.Max(s.MousePixels, Settings.MinMousePixels), Settings.MaxMousePixels);
            _smart.Checked = s.SmartIdle;
            _awake.Checked = s.KeepAwake;
            _teamsOnly.Checked = s.OnlyWhileTeamsRuns;
            _maxRun.Value = Math.Min(Math.Max(s.MaxRuntimeHours, 0), Settings.MaxRuntimeHoursLimit);
            UpdateEnabled();
            UpdateIntervalHint();
        }

        void UpdateEnabled()
        {
            bool mouse = S != null && S.Mode != ActivityMode.Key;
            bool key = S != null && S.Mode != ActivityMode.Mouse;
            _pixelsRow.Enabled = mouse;
            _keyRow.Enabled = key;
        }

        /// <summary>Teams wird nach etwa 5 Minuten abwesend: Ab 4 Minuten Abstand warnt der Hinweis in Rot.</summary>
        void UpdateIntervalHint()
        {
            int seconds = (int)_interval.Value;
            bool risky = Settings.IsIntervalRisky(seconds);
            _intervalRow.Caption = risky ? Loc.T("act.interval.warn", seconds) : Loc.T("act.hint");
            _intervalRow.CaptionColor = risky ? Theme.Red : Theme.Muted;
            RefreshLayout();
        }

        public override void ApplyTexts()
        {
            Quiet(() =>
            {
                int selected = _mode.SelectedIndex;
                _mode.Items.Clear();
                _mode.Items.Add(Loc.T("act.mode.key"));
                _mode.Items.Add(Loc.T("act.mode.mouse"));
                _mode.Items.Add(Loc.T("act.mode.both"));
                _mode.SelectedIndex = selected >= 0 ? selected : (int)ActivityMode.Both;

                // Die Reihenfolge entspricht ActivityKey: F13 bis F24, dann die Umschalttaste.
                int selectedKey = _key.SelectedIndex;
                _key.Items.Clear();
                for (int i = 0; i < (int)ActivityKey.Shift; i++) _key.Items.Add(((ActivityKey)i).ToString());
                _key.Items.Add(Loc.T("act.key.shift"));
                _key.SelectedIndex = selectedKey >= 0 ? selectedKey : (int)ActivityKey.F15;
            });

            _methodCard.Text = Loc.T("act.grp.method");
            _modeRow.Title = Loc.T("act.mode");
            _keyRow.Title = Loc.T("act.key");
            _keyRow.Caption = Loc.T("act.key.hint");
            _intervalRow.Title = Loc.T("act.interval");
            _secondsUnit.Text = Loc.T("act.unit.sec");
            _pixelsRow.Title = Loc.T("act.pixels");
            _pixelsRow.Caption = Loc.T("act.pixels.hint");
            _pixelsUnit.Text = Loc.T("act.unit.px");
            _testRow.Title = Loc.T("act.test.title");
            _testRow.Caption = Loc.T("act.test.hint");
            _testRow.CaptionColor = Theme.Muted;
            _test.Text = Loc.T("act.test");

            _behaviorCard.Text = Loc.T("act.grp.behavior");
            _smartRow.Title = Loc.T("act.smart");
            _smartRow.Caption = Loc.T("act.smart.hint");
            _awakeRow.Title = Loc.T("act.awake");
            _awakeRow.Caption = Loc.T("act.awake.hint");
            _teamsRow.Title = Loc.T("act.teamsonly");
            _teamsRow.Caption = Loc.T("act.teamsonly.hint");
            _maxRunRow.Title = Loc.T("act.maxrun");
            _maxRunRow.Caption = Loc.T("act.maxrun.hint");
            _hoursUnit.Text = Loc.T("act.maxrun.unit");

            UpdateIntervalHint();
            RefreshLayout();
        }
    }
}
