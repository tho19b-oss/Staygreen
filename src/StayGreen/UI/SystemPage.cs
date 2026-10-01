using System;
using System.IO;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    enum HotkeyState
    {
        Off,
        Active,
        Failed,
    }

    /// <summary>Autostart, Tray, Protokoll, Hotkey, Sprache.</summary>
    sealed class SystemPage : PageBase
    {
        readonly CheckBox _startOnLaunch = CreateCheck();
        readonly CheckBox _autostart = CreateCheck();
        readonly CheckBox _startMin = CreateCheck();
        readonly CheckBox _tray = CreateCheck();

        readonly GroupBox _logGroup;
        readonly CheckBox _log = CreateCheck();
        readonly TextBox _logPath = new TextBox();
        readonly Button _browse = new Button { Text = "…", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        readonly Button _openLog = new Button { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6, 0, 6, 0) };

        readonly GroupBox _hotkeyGroup;
        readonly CheckBox _hotkey = CreateCheck();
        readonly CheckBox _ctrl = CreateCheck();
        readonly CheckBox _alt = CreateCheck();
        readonly CheckBox _shift = CreateCheck();
        readonly CheckBox _win = CreateCheck();
        readonly ComboBox _key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 64 };
        readonly WrapLabel _hotkeyStatus = CreateHint();

        readonly Label _langLabel = CreateLabel();
        readonly ComboBox _lang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        readonly Button _openFolder = new Button { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(8, 1, 8, 1) };
        readonly WrapLabel _portable = CreateHint();

        /// <summary>Standardpfad des Protokolls (vom Hauptfenster geliefert).</summary>
        public Func<string> DefaultLogPath = () => "";

        public Action OpenFolderRequested;
        public Action OpenLogRequested;
        public bool IsPortable;

        public SystemPage()
        {
            TableLayoutPanel root = CreateRoot();

            AddRow(root, _startOnLaunch);
            AddRow(root, _autostart);
            AddRow(root, _startMin);
            AddRow(root, _tray);

            // Protokoll
            var logGrid = CreateGrid(3);
            logGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            logGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            logGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            logGrid.Controls.Add(_log, 0, 0);
            logGrid.SetColumnSpan(_log, 3);
            _logPath.Dock = DockStyle.Fill;
            _logPath.Margin = new Padding(3, 4, 3, 3);
            _browse.Margin = new Padding(3, 3, 3, 3);
            _openLog.Margin = new Padding(3, 3, 3, 3);
            logGrid.Controls.Add(_logPath, 0, 1);
            logGrid.Controls.Add(_browse, 1, 1);
            logGrid.Controls.Add(_openLog, 2, 1);
            _logGroup = CreateGroup(logGrid);
            AddRow(root, _logGroup);

            // Hotkey
            var hotkeyGrid = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1 };
            hotkeyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            var combo = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Dock = DockStyle.Fill };
            foreach (CheckBox box in new[] { _ctrl, _alt, _shift, _win })
            {
                box.Margin = new Padding(3, 6, 8, 3);
                combo.Controls.Add(box);
            }
            _key.Margin = new Padding(3, 3, 3, 3);
            foreach (string k in Settings.HotkeyKeys) _key.Items.Add(k);
            combo.Controls.Add(_key);
            hotkeyGrid.Controls.Add(_hotkey);
            hotkeyGrid.Controls.Add(combo);
            hotkeyGrid.Controls.Add(_hotkeyStatus);
            _hotkeyStatus.Dock = DockStyle.Fill;
            _hotkeyGroup = CreateGroup(hotkeyGrid);
            AddRow(root, _hotkeyGroup);

            // Sprache + Ordner
            var langRow = CreateGrid(2);
            langRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            langRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _lang.Margin = new Padding(3, 3, 3, 3);
            langRow.Controls.Add(_langLabel, 0, 0);
            langRow.Controls.Add(_lang, 1, 0);
            AddRow(root, langRow);

            var folderRow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true };
            _openFolder.Margin = new Padding(3, 3, 3, 3);
            folderRow.Controls.Add(_openFolder);
            AddRow(root, folderRow);
            AddRow(root, _portable);

            _startOnLaunch.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartHoldingOnLaunch = _startOnLaunch.Checked; Fire(); } };
            _autostart.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartWithWindows = _autostart.Checked; Fire(); } };
            _startMin.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartMinimized = _startMin.Checked; Fire(); } };
            _tray.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.MinimizeToTray = _tray.Checked; Fire(); } };

            _log.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.LogEnabled = _log.Checked;
                UpdateEnabled();
                Fire();
            };
            _logPath.Leave += (o, e) => CommitLogPath();
            _logPath.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    CommitLogPath();
                    e.SuppressKeyPress = true;
                }
            };
            _browse.Click += (o, e) => BrowseLog();
            _openLog.Click += (o, e) => { if (OpenLogRequested != null) OpenLogRequested(); };

            _hotkey.CheckedChanged += (o, e) =>
            {
                if (Loading || S == null) return;
                S.HotkeyEnabled = _hotkey.Checked;
                UpdateEnabled();
                Fire();
            };
            _ctrl.CheckedChanged += (o, e) => HotkeyEdited();
            _alt.CheckedChanged += (o, e) => HotkeyEdited();
            _shift.CheckedChanged += (o, e) => HotkeyEdited();
            _win.CheckedChanged += (o, e) => HotkeyEdited();
            _key.SelectedIndexChanged += (o, e) => HotkeyEdited();

            _lang.SelectedIndexChanged += (o, e) =>
            {
                if (Loading || S == null || _lang.SelectedIndex < 0) return;
                S.Language = _lang.SelectedIndex == 1 ? "de" : (_lang.SelectedIndex == 2 ? "en" : "auto");
                Fire();
            };
            _openFolder.Click += (o, e) => { if (OpenFolderRequested != null) OpenFolderRequested(); };
        }

        void HotkeyEdited()
        {
            if (Loading || S == null) return;
            S.HotkeyCtrl = _ctrl.Checked;
            S.HotkeyAlt = _alt.Checked;
            S.HotkeyShift = _shift.Checked;
            S.HotkeyWin = _win.Checked;
            if (_key.SelectedItem != null) S.HotkeyKey = (string)_key.SelectedItem;
            Fire();
        }

        void CommitLogPath()
        {
            if (Loading || S == null) return;
            string text = _logPath.Text.Trim();
            string normalized = text == DefaultLogPath() ? "" : text;
            if (normalized == S.LogPath) return;
            S.LogPath = normalized;
            Fire();
        }

        void BrowseLog()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = Loc.T("sys.log.browse.title");
                dialog.Filter = Loc.T("sys.log.browse.filter");
                dialog.OverwritePrompt = false;
                string current = _logPath.Text.Trim();
                try
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        dialog.InitialDirectory = Path.GetDirectoryName(current);
                        dialog.FileName = Path.GetFileName(current);
                    }
                }
                catch
                {
                    // Ungueltiger Pfad im Textfeld: Dialog mit Standardwerten oeffnen.
                }
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
                _logPath.Text = dialog.FileName;
                CommitLogPath();
            }
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _startOnLaunch.Checked = s.StartHoldingOnLaunch;
            _autostart.Checked = s.StartWithWindows;
            _startMin.Checked = s.StartMinimized;
            _tray.Checked = s.MinimizeToTray;
            _log.Checked = s.LogEnabled;
            _logPath.Text = string.IsNullOrEmpty(s.LogPath) ? DefaultLogPath() : s.LogPath;
            _hotkey.Checked = s.HotkeyEnabled;
            _ctrl.Checked = s.HotkeyCtrl;
            _alt.Checked = s.HotkeyAlt;
            _shift.Checked = s.HotkeyShift;
            _win.Checked = s.HotkeyWin;
            int keyIndex = Array.IndexOf(Settings.HotkeyKeys, s.HotkeyKey);
            _key.SelectedIndex = keyIndex >= 0 ? keyIndex : 6;
            _lang.SelectedIndex = s.Language == "de" ? 1 : (s.Language == "en" ? 2 : 0);
            UpdateEnabled();
        }

        void UpdateEnabled()
        {
            bool log = _log.Checked;
            _logPath.Enabled = log;
            _browse.Enabled = log;
            bool hotkey = _hotkey.Checked;
            _ctrl.Enabled = hotkey;
            _alt.Enabled = hotkey;
            _shift.Enabled = hotkey;
            _win.Enabled = hotkey;
            _key.Enabled = hotkey;
        }

        public void SetHotkeyStatus(HotkeyState state, string description)
        {
            switch (state)
            {
                case HotkeyState.Active:
                    _hotkeyStatus.Text = Loc.T("sys.hotkey.ok", description);
                    _hotkeyStatus.ForeColor = Theme.Green;
                    break;
                case HotkeyState.Failed:
                    _hotkeyStatus.Text = Loc.T("sys.hotkey.fail", description);
                    _hotkeyStatus.ForeColor = Theme.Red;
                    break;
                default:
                    _hotkeyStatus.Text = "";
                    break;
            }
        }

        public override void ApplyTexts()
        {
            _startOnLaunch.Text = Loc.T("sys.startonlaunch");
            _autostart.Text = Loc.T("sys.autostart");
            _startMin.Text = Loc.T("sys.startmin");
            _tray.Text = Loc.T("sys.tray");
            _logGroup.Text = Loc.T("sys.grp.log");
            _log.Text = Loc.T("sys.log");
            _openLog.Text = Loc.T("sys.log.open");
            _hotkeyGroup.Text = Loc.T("sys.grp.hotkey");
            _hotkey.Text = Loc.T("sys.hotkey");
            _ctrl.Text = Loc.T("key.ctrl");
            _alt.Text = Loc.T("key.alt");
            _shift.Text = Loc.T("key.shift");
            _win.Text = Loc.T("key.win");
            _langLabel.Text = Loc.T("sys.language");
            _openFolder.Text = Loc.T("sys.openfolder");
            _portable.Text = IsPortable ? Loc.T("sys.portable") : "";

            Quiet(() =>
            {
                int selected = _lang.SelectedIndex;
                _lang.Items.Clear();
                _lang.Items.Add(Loc.T("sys.lang.auto"));
                _lang.Items.Add("Deutsch");
                _lang.Items.Add("English");
                _lang.SelectedIndex = selected >= 0 ? selected : 0;
            });
            RefreshLayout();
        }
    }
}
