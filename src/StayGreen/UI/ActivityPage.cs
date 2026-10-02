using System;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>Methode, Intervall, intelligenter Modus und Wach-Halten.</summary>
    sealed class ActivityPage : PageBase
    {
        readonly Card _methodCard = new Card();
        readonly Card _behaviorCard = new Card();

        readonly ComboBox _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly NumericUpDown _interval = new NumericUpDown { Minimum = Settings.MinInterval, Maximum = Settings.MaxInterval };
        readonly NumericUpDown _pixels = new NumericUpDown { Minimum = Settings.MinMousePixels, Maximum = Settings.MaxMousePixels };
        readonly Label _secondsUnit = CreateLabel();
        readonly Label _pixelsUnit = CreateLabel();
        readonly SwitchBox _smart = CreateSwitch();
        readonly SwitchBox _awake = CreateSwitch();
        readonly FlatButton _test = CreateButton(ButtonKind.Secondary);

        readonly SettingRow _modeRow;
        readonly SettingRow _intervalRow;
        readonly SettingRow _pixelsRow;
        readonly SettingRow _testRow;
        readonly SettingRow _smartRow;
        readonly SettingRow _awakeRow;

        /// <summary>Wird vom Hauptfenster gesetzt: erzeugt eine Eingabe und meldet, ob Windows sie angenommen hat.</summary>
        public Func<bool> TestRequested;

        public ActivityPage()
        {
            _mode.Width = Dpi.Px(230);
            _interval.Width = Dpi.Px(76);
            _pixels.Width = Dpi.Px(76);

            _modeRow = new SettingRow(_mode);
            _intervalRow = new SettingRow(new InlineRow(_interval, _secondsUnit));
            _pixelsRow = new SettingRow(new InlineRow(_pixels, _pixelsUnit));
            _testRow = new SettingRow(_test);
            _smartRow = new SettingRow(_smart);
            _awakeRow = new SettingRow(_awake);

            _methodCard.AddRow(_modeRow);
            _methodCard.AddRow(_intervalRow);
            _methodCard.AddRow(_pixelsRow);
            _methodCard.AddRow(_testRow);
            _behaviorCard.AddRow(_smartRow);
            _behaviorCard.AddRow(_awakeRow);
            Controls.Add(_methodCard);
            Controls.Add(_behaviorCard);

            _mode.SelectedIndexChanged += (o, e) =>
            {
                if (Loading || S == null || _mode.SelectedIndex < 0) return;
                S.Mode = (ActivityMode)_mode.SelectedIndex;
                UpdateEnabled();
                Fire();
            };
            _interval.ValueChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.IntervalSeconds = (int)_interval.Value;
                Fire();
            };
            _pixels.ValueChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.MousePixels = (int)_pixels.Value;
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
            _test.Click += (o, e) =>
            {
                bool ok = TestRequested != null && TestRequested();
                _testRow.Caption = Loc.T(ok ? "act.test.ok" : "act.test.fail");
                _testRow.CaptionColor = ok ? Theme.GreenButton : Theme.Red;
                RefreshLayout();
            };
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _mode.SelectedIndex = (int)s.Mode;
            _interval.Value = Math.Min(Math.Max(s.IntervalSeconds, Settings.MinInterval), Settings.MaxInterval);
            _pixels.Value = Math.Min(Math.Max(s.MousePixels, Settings.MinMousePixels), Settings.MaxMousePixels);
            _smart.Checked = s.SmartIdle;
            _awake.Checked = s.KeepAwake;
            UpdateEnabled();
        }

        void UpdateEnabled()
        {
            bool mouse = S != null && S.Mode != ActivityMode.Key;
            _pixelsRow.Enabled = mouse;
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
            });

            _methodCard.Text = Loc.T("act.grp.method");
            _modeRow.Title = Loc.T("act.mode");
            _intervalRow.Title = Loc.T("act.interval");
            _intervalRow.Caption = Loc.T("act.hint");
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
            RefreshLayout();
        }
    }
}
