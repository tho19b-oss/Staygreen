using System;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>Methode, Intervall, intelligenter Modus und Wach-Halten.</summary>
    sealed class ActivityPage : PageBase
    {
        readonly GroupBox _group;
        readonly Label _modeLabel = CreateLabel();
        readonly Label _intervalLabel = CreateLabel();
        readonly Label _pixelsLabel = CreateLabel();
        readonly Label _secondsUnit = CreateLabel();
        readonly Label _pixelsUnit = CreateLabel();
        readonly ComboBox _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        readonly NumericUpDown _interval = new NumericUpDown { Minimum = Settings.MinInterval, Maximum = Settings.MaxInterval, Width = 72 };
        readonly NumericUpDown _pixels = new NumericUpDown { Minimum = Settings.MinMousePixels, Maximum = Settings.MaxMousePixels, Width = 72 };
        readonly WrapLabel _intervalHint = CreateHint();
        readonly CheckBox _smart = CreateCheck();
        readonly WrapLabel _smartHint = CreateHint();
        readonly CheckBox _awake = CreateCheck();
        readonly WrapLabel _awakeHint = CreateHint();
        readonly Button _test = new Button { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 2, 10, 2) };
        readonly WrapLabel _testResult = new WrapLabel();

        /// <summary>Wird vom Hauptfenster gesetzt: erzeugt eine Eingabe und meldet, ob Windows sie angenommen hat.</summary>
        public Func<bool> TestRequested;

        public ActivityPage()
        {
            TableLayoutPanel root = CreateRoot();

            TableLayoutPanel grid = CreateGrid(3);
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            _mode.Dock = DockStyle.Fill;
            _mode.Margin = new Padding(3, 3, 3, 3);
            _interval.Anchor = AnchorStyles.Left;
            _pixels.Anchor = AnchorStyles.Left;

            grid.Controls.Add(_modeLabel, 0, 0);
            grid.Controls.Add(_mode, 1, 0);
            grid.SetColumnSpan(_mode, 2);
            grid.Controls.Add(_intervalLabel, 0, 1);
            grid.Controls.Add(_interval, 1, 1);
            grid.Controls.Add(_secondsUnit, 2, 1);
            grid.Controls.Add(_pixelsLabel, 0, 2);
            grid.Controls.Add(_pixels, 1, 2);
            grid.Controls.Add(_pixelsUnit, 2, 2);

            _group = CreateGroup(grid);
            AddRow(root, _group);
            AddRow(root, _intervalHint);
            AddRow(root, _smart);
            AddRow(root, _smartHint);
            AddRow(root, _awake);
            AddRow(root, _awakeHint);

            TableLayoutPanel testRow = CreateGrid(2);
            testRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            testRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _test.Margin = new Padding(3, 6, 8, 3);
            _testResult.Anchor = AnchorStyles.Left;
            _testResult.Margin = new Padding(3, 9, 3, 3);
            testRow.Controls.Add(_test, 0, 0);
            testRow.Controls.Add(_testResult, 1, 0);
            AddRow(root, testRow);

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
                _testResult.Text = Loc.T(ok ? "act.test.ok" : "act.test.fail");
                _testResult.ForeColor = ok ? Theme.Green : Theme.Red;
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
            _pixels.Enabled = mouse;
            _pixelsLabel.Enabled = mouse;
            _pixelsUnit.Enabled = mouse;
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

            _group.Text = Loc.T("act.grp.method");
            _modeLabel.Text = Loc.T("act.mode");
            _intervalLabel.Text = Loc.T("act.interval");
            _secondsUnit.Text = Loc.T("act.unit.sec");
            _pixelsLabel.Text = Loc.T("act.pixels");
            _pixelsUnit.Text = Loc.T("act.unit.px");
            _intervalHint.Text = Loc.T("act.hint");
            _smart.Text = Loc.T("act.smart");
            _smartHint.Text = Loc.T("act.smart.hint");
            _awake.Text = Loc.T("act.awake");
            _awakeHint.Text = Loc.T("act.awake.hint");
            _test.Text = Loc.T("act.test");
            _testResult.Text = "";
            RefreshLayout();
        }
    }
}
