using System;
using System.Drawing;
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

        /// <summary>Die Kombination wuerde auf der Tastatur ein Zeichen tippen (AltGr) und wird deshalb nicht angemeldet.</summary>
        Conflict,
    }

    /// <summary>Start, Tastenkombination, Protokoll, Sprache, Darstellung.</summary>
    sealed class SystemPage : PageBase
    {
        readonly Card _startCard = new Card();
        readonly Card _hotkeyCard = new Card();
        readonly Card _logCard = new Card();
        readonly Card _generalCard = new Card();

        readonly SwitchBox _startOnLaunch = CreateSwitch();
        readonly SwitchBox _autostart = CreateSwitch();
        readonly SwitchBox _startMin = CreateSwitch();
        readonly SwitchBox _tray = CreateSwitch();
        readonly SettingRow _startOnLaunchRow;
        readonly SettingRow _autostartRow;
        readonly SettingRow _startMinRow;
        readonly SettingRow _trayRow;

        readonly SwitchBox _hotkey = CreateSwitch();
        readonly SettingRow _hotkeyRow;
        readonly ChipBox _ctrl = new ChipBox();
        readonly ChipBox _alt = new ChipBox();
        readonly ChipBox _shift = new ChipBox();
        readonly ChipBox _win = new ChipBox();
        readonly ComboBox _key = Style(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.Px(68) });
        readonly Stack _comboRow;

        readonly SwitchBox _log = CreateSwitch();
        readonly SettingRow _logRow;
        readonly TextBox _logPath = Style(new TextBox { BorderStyle = BorderStyle.FixedSingle });
        readonly FlatButton _browse = CreateButton(ButtonKind.Secondary);
        readonly FlatButton _openLog = CreateButton(ButtonKind.Secondary);
        readonly Stack _logPathRow;
        readonly Stack _logButtonRow;

        readonly ComboBox _lang = Style(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.Px(200) });
        readonly SettingRow _langRow;
        readonly ComboBox _theme = Style(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.Px(200) });
        readonly SettingRow _themeRow;
        readonly FlatButton _openFolder = CreateButton(ButtonKind.Secondary);
        readonly SettingRow _folderRow;
        readonly FlatButton _checkUpdate = CreateButton(ButtonKind.Secondary);
        readonly SettingRow _updateRow;

        string _settingsError;
        string _logError;

        /// <summary>Standardpfad des Protokolls (vom Hauptfenster geliefert).</summary>
        public Func<string> DefaultLogPath = () => "";

        public Action OpenFolderRequested;
        public Action OpenLogRequested;
        public Action OpenReleasePageRequested;
        public bool IsPortable;

        public SystemPage()
        {
            // Start
            _startOnLaunchRow = new SettingRow(_startOnLaunch);
            _autostartRow = new SettingRow(_autostart);
            _startMinRow = new SettingRow(_startMin);
            _trayRow = new SettingRow(_tray);
            _startCard.AddRow(_startOnLaunchRow);
            _startCard.AddRow(_autostartRow);
            _startCard.AddRow(_startMinRow);
            _startCard.AddRow(_trayRow);

            // Tastenkombination
            _hotkeyRow = new SettingRow(_hotkey);
            foreach (string k in Settings.HotkeyKeys) _key.Items.Add(k);
            var combo = new InlineRow(_ctrl, _alt, _shift, _win, _key) { Gap = 8 };
            _comboRow = Pad(combo, 2, 12);
            _hotkeyCard.AddRow(_hotkeyRow);
            _hotkeyCard.Add(_comboRow);

            // Protokoll
            _logRow = new SettingRow(_log);
            _logPathRow = Pad(_logPath, 2, 6);
            _logButtonRow = Pad(new InlineRow(_browse, _openLog), 0, 10);
            _logCard.AddRow(_logRow);
            _logCard.Add(_logPathRow);
            _logCard.Add(_logButtonRow);

            // Sprache, Darstellung und Daten
            _langRow = new SettingRow(_lang);
            _themeRow = new SettingRow(_theme);
            _folderRow = new SettingRow(_openFolder);
            _updateRow = new SettingRow(_checkUpdate);
            _generalCard.AddRow(_langRow);
            _generalCard.AddRow(_themeRow);
            _generalCard.AddRow(_folderRow);
            _generalCard.AddRow(_updateRow);

            Controls.Add(_startCard);
            Controls.Add(_hotkeyCard);
            Controls.Add(_logCard);
            Controls.Add(_generalCard);

            _startOnLaunch.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartHoldingOnLaunch = _startOnLaunch.Checked; Fire(); } };
            _autostart.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartWithWindows = _autostart.Checked; Fire(); } };
            _startMin.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartMinimized = _startMin.Checked; Fire(); } };
            _tray.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.MinimizeToTray = _tray.Checked; Fire(); } };

            _log.CheckedChanged += (o, e) =>
            {
                UpdateEnabled();
                if (Loading || S == null) return;
                S.LogEnabled = _log.Checked;
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
                UpdateEnabled();
                if (Loading || S == null) return;
                S.HotkeyEnabled = _hotkey.Checked;
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
            _theme.SelectedIndexChanged += (o, e) =>
            {
                if (Loading || S == null || _theme.SelectedIndex < 0) return;
                S.Theme = _theme.SelectedIndex == 1 ? "light" : (_theme.SelectedIndex == 2 ? "dark" : "auto");
                Fire();
            };
            _openFolder.Click += (o, e) => { if (OpenFolderRequested != null) OpenFolderRequested(); };
            _checkUpdate.Click += (o, e) => { if (OpenReleasePageRequested != null) OpenReleasePageRequested(); };
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
            _theme.SelectedIndex = s.Theme == "light" ? 1 : (s.Theme == "dark" ? 2 : 0);
            UpdateEnabled();
        }

        void UpdateEnabled()
        {
            bool log = _log.Checked;
            _logPathRow.Enabled = log;
            _browse.Enabled = log;
            _comboRow.Enabled = _hotkey.Checked;
        }

        /// <summary>Zeigt, ob die Tastenkombination aktiv ist, belegt ist oder auf der Tastatur ein Zeichen tippen wuerde.</summary>
        /// <param name="character">Bei <see cref="HotkeyState.Conflict"/> das Zeichen, das getippt wuerde.</param>
        public void SetHotkeyStatus(HotkeyState state, string description, string character = null)
        {
            switch (state)
            {
                case HotkeyState.Active:
                    _hotkeyRow.Caption = Loc.T("sys.hotkey.ok", description);
                    _hotkeyRow.CaptionColor = Theme.GreenButton;
                    break;
                case HotkeyState.Failed:
                    _hotkeyRow.Caption = Loc.T("sys.hotkey.fail", description);
                    _hotkeyRow.CaptionColor = Theme.Red;
                    break;
                case HotkeyState.Conflict:
                    _hotkeyRow.Caption = Loc.T("sys.hotkey.conflict", description, character ?? "?");
                    _hotkeyRow.CaptionColor = Theme.Red;
                    break;
                default:
                    _hotkeyRow.Caption = "";
                    break;
            }
            RefreshLayout();
        }

        /// <summary>
        /// Zeigt Speicher- und Protokollfehler dauerhaft an der jeweiligen Einstellung (null = alles in Ordnung),
        /// damit ein nicht beschreibbarer Ordner nicht still Einstellungen verschluckt.
        /// </summary>
        public void SetStorageStatus(string settingsError, string logError)
        {
            _settingsError = settingsError;
            _logError = logError;
            ApplyStorageCaptions();
            RefreshLayout();
        }

        void ApplyStorageCaptions()
        {
            if (_settingsError != null)
            {
                _folderRow.Caption = Loc.T("sys.storage.fail", _settingsError);
                _folderRow.CaptionColor = Theme.Red;
            }
            else
            {
                _folderRow.Caption = IsPortable ? Loc.T("sys.portable") : "";
                _folderRow.CaptionColor = Theme.Muted;
            }

            if (_logError != null)
            {
                _logRow.Caption = Loc.T("sys.log.fail", _logError);
                _logRow.CaptionColor = Theme.Red;
            }
            else
            {
                _logRow.Caption = Loc.T("sys.log.hint");
                _logRow.CaptionColor = Theme.Muted;
            }
        }

        public override void ApplyTexts()
        {
            _startCard.Text = Loc.T("sys.grp.start");
            _startOnLaunchRow.Title = Loc.T("sys.startonlaunch");
            _autostartRow.Title = Loc.T("sys.autostart");
            _autostartRow.Caption = Loc.T("sys.autostart.hint");
            _startMinRow.Title = Loc.T("sys.startmin");
            _trayRow.Title = Loc.T("sys.tray");

            _hotkeyCard.Text = Loc.T("sys.grp.hotkey");
            _hotkeyRow.Title = Loc.T("sys.hotkey");
            _ctrl.Text = Loc.T("key.ctrl");
            _alt.Text = Loc.T("key.alt");
            _shift.Text = Loc.T("key.shift");
            _win.Text = Loc.T("key.win");
            _key.AccessibleName = Loc.T("sys.hotkey");

            _logCard.Text = Loc.T("sys.grp.log");
            _logRow.Title = Loc.T("sys.log");
            _logPath.AccessibleName = Loc.T("sys.log");
            _browse.Text = Loc.T("sys.log.change");
            _openLog.Text = Loc.T("sys.log.open");

            _generalCard.Text = Loc.T("sys.grp.general");
            _langRow.Title = Loc.T("sys.language");
            _themeRow.Title = Loc.T("sys.theme");
            _folderRow.Title = Loc.T("sys.folder");
            _openFolder.Text = Loc.T("sys.log.open");
            _updateRow.Title = Loc.T("sys.update");
            _updateRow.Caption = Loc.T("sys.update.hint");
            _checkUpdate.Text = Loc.T("sys.update.open");
            ApplyStorageCaptions();

            Quiet(() =>
            {
                int selected = _lang.SelectedIndex;
                _lang.Items.Clear();
                _lang.Items.Add(Loc.T("sys.lang.auto"));
                _lang.Items.Add("Deutsch");
                _lang.Items.Add("English");
                _lang.SelectedIndex = selected >= 0 ? selected : 0;

                int selectedTheme = _theme.SelectedIndex;
                _theme.Items.Clear();
                _theme.Items.Add(Loc.T("sys.theme.auto"));
                _theme.Items.Add(Loc.T("sys.theme.light"));
                _theme.Items.Add(Loc.T("sys.theme.dark"));
                _theme.SelectedIndex = selectedTheme >= 0 ? selectedTheme : 0;
            });
            RefreshLayout();
        }
    }
}
