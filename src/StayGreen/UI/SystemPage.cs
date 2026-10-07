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

        /// <summary>Die Kombination wuerde auf der Tastatur ein Zeichen tippen (AltGr) und wird deshalb nicht angemeldet.</summary>
        Conflict,
    }

    /// <summary>
    /// Start (Schalter), Tastenkombination und Protokoll (Kopf mit Schalter, darunter eine Wertzeile zum Aufklappen),
    /// Sprache und Darstellung (Auswahl, die sofort gilt) sowie Einstellungsordner und Versionssuche.
    /// </summary>
    sealed class SystemPage : PageBase
    {
        readonly Card _startCard = new Card();
        readonly SwitchBox _startOnLaunch = CreateSwitch();
        readonly SwitchBox _autostart = CreateSwitch();
        readonly SwitchBox _startMin = CreateSwitch();
        readonly SwitchBox _tray = CreateSwitch();
        readonly SettingRow _startOnLaunchRow;
        readonly SettingRow _autostartRow;
        readonly SettingRow _startMinRow;
        readonly SettingRow _trayRow;

        readonly Card _hotkeyCard = new Card();
        readonly SwitchBox _hotkey = CreateSwitch();
        readonly SettingRow _hotkeyHeader;
        readonly WrapLabel _hotkeyOff = CreateHint();
        readonly Stack _hotkeyOffRow;
        readonly Stack _hotkeyBody = new Stack();
        readonly ListStack _hotkeyList = new ListStack { TrailingDivider = false };
        readonly EditableRow _comboRow = new EditableRow(EditorKind.Input);
        readonly ChipBox _ctrl = new ChipBox();
        readonly ChipBox _alt = new ChipBox();
        readonly ChipBox _shift = new ChipBox();
        readonly ChipBox _win = new ChipBox();
        readonly Label _plus = CreateEditorLabel();
        readonly ComboBox _key = Style(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Dpi.Px(68) });
        readonly WrapLabel _hotkeyStatus = new WrapLabel { ForeColor = Theme.DangerText };
        readonly Stack _hotkeyStatusRow;

        readonly Card _logCard = new Card();
        readonly SwitchBox _log = CreateSwitch();
        readonly SettingRow _logHeader;
        readonly WrapLabel _logOff = CreateHint();
        readonly Stack _logOffRow;
        readonly Stack _logBody = new Stack();
        readonly ListStack _logList = new ListStack();
        readonly EditableRow _fileRow = new EditableRow(EditorKind.Input);
        readonly TextBox _logPath = Style(new TextBox { BorderStyle = BorderStyle.FixedSingle });
        readonly FlatButton _browse = CreateButton(ButtonKind.Secondary);
        readonly FlatButton _useDefault = CreateButton(ButtonKind.Secondary);
        readonly WrapLabel _logPathHint = CreateEditorHint();
        readonly WrapLabel _logError = new WrapLabel { ForeColor = Theme.DangerText };
        readonly Stack _logErrorRow;
        readonly FlatButton _openLog = CreateButton(ButtonKind.Secondary);

        readonly Card _generalCard = new Card();
        readonly SettingRow _generalHeader = CreateHeader(null);
        readonly ListStack _generalList = new ListStack();
        readonly EditableRow _langRow = new EditableRow(EditorKind.Choice);
        readonly Segmented _lang = new Segmented();
        readonly WrapLabel _langHint = CreateEditorHint();
        readonly EditableRow _themeRow = new EditableRow(EditorKind.Choice);
        readonly Segmented _theme = new Segmented();
        readonly WrapLabel _themeHint = CreateEditorHint();
        readonly FlatButton _openFolder = CreateButton(ButtonKind.Secondary);
        readonly FlatButton _checkUpdate = CreateButton(ButtonKind.Secondary);
        readonly WrapLabel _updateHint = CreateHint();
        readonly WrapLabel _storage = CreateHint();
        readonly Stack _storageRow;

        string _settingsError;
        string _logErrorText;

        /// <summary>Standardpfad des Protokolls (vom Hauptfenster geliefert).</summary>
        public Func<string> DefaultLogPath = () => "";

        /// <summary>
        /// Welches Zeichen eine Kombination auf dieser Tastatur tippen wuerde (AltGr), sonst null. Vom Hauptfenster gesetzt;
        /// damit sperrt der Editor "Speichern", bevor eine solche Kombination ueberhaupt angemeldet wird.
        /// </summary>
        public Func<Settings, string> TypedCharacter;

        public Action OpenFolderRequested;
        public Action OpenLogRequested;
        public Action OpenReleasePageRequested;
        public bool IsPortable;

        public SystemPage()
        {
            // Start: reine An/Aus-Schalter
            _startOnLaunchRow = new SettingRow(_startOnLaunch);
            _autostartRow = new SettingRow(_autostart);
            _startMinRow = new SettingRow(_startMin);
            _trayRow = new SettingRow(_tray);
            _startCard.AddRow(_startOnLaunchRow);
            _startCard.AddRow(_autostartRow);
            _startCard.AddRow(_startMinRow);
            _startCard.AddRow(_trayRow);

            // Tastenkombination: Kopf mit Schalter, darunter die Kombination als Wertzeile
            _hotkeyCard.Text = "";
            _hotkeyHeader = CreateHeader(_hotkey);
            _hotkeyCard.AddRow(_hotkeyHeader);
            _hotkeyOffRow = Pad(_hotkeyOff, 0, 2);
            _hotkeyCard.Add(_hotkeyOffRow);
            foreach (string k in Settings.HotkeyKeys) _key.Items.Add(k);
            _comboRow.AddContent(new FlowRow(_ctrl, _alt, _shift, _win, _plus, _key));
            _hotkeyList.Controls.Add(Track(_comboRow));
            _hotkeyStatusRow = Pad(_hotkeyStatus, 8, 2);
            _hotkeyStatusRow.Visible = false;
            _hotkeyBody.Controls.Add(_hotkeyList);
            _hotkeyBody.Controls.Add(_hotkeyStatusRow);
            _hotkeyCard.Add(_hotkeyBody);

            // Protokoll: Kopf mit Schalter, darunter die Datei als Wertzeile und "Protokoll oeffnen"
            _logCard.Text = "";
            _logHeader = CreateHeader(_log);
            _logCard.AddRow(_logHeader);
            _logOffRow = Pad(_logOff, 0, 2);
            _logCard.Add(_logOffRow);
            _fileRow.AddContent(_logPath);
            _fileRow.AddContent(new FlowRow(_browse, _useDefault));
            _fileRow.AddContent(_logPathHint);
            _fileRow.FocusTarget = _logPath;
            _logList.Controls.Add(Track(_fileRow));
            _logErrorRow = Pad(_logError, 8, 0);
            _logErrorRow.Visible = false;
            _logBody.Controls.Add(_logList);
            _logBody.Controls.Add(_logErrorRow);
            _logBody.Controls.Add(new FlowRow(_openLog) { Inset = new Padding(0, 10, 0, 2) });
            _logCard.Add(_logBody);

            // Sprache und Daten: zwei Auswahlen, die sofort gelten, dann Ordner und Versionssuche
            _generalCard.Text = "";
            _generalCard.AddRow(_generalHeader);
            _langRow.AddContent(_lang);
            _langRow.AddContent(_langHint);
            _themeRow.AddContent(_theme);
            _themeRow.AddContent(_themeHint);
            _generalList.Controls.Add(Track(_langRow));
            _generalList.Controls.Add(Track(_themeRow));
            _generalCard.Add(_generalList);
            _generalCard.Add(new FlowRow(_openFolder, _checkUpdate) { Inset = new Padding(0, 10, 0, 0) });
            _generalCard.Add(Pad(_updateHint, 8, 0));
            _storageRow = Pad(_storage, 4, 2);
            _generalCard.Add(_storageRow);

            Controls.Add(_startCard);
            Controls.Add(_hotkeyCard);
            Controls.Add(_logCard);
            Controls.Add(_generalCard);

            _startOnLaunch.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartHoldingOnLaunch = _startOnLaunch.Checked; Fire(); } };
            _autostart.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartWithWindows = _autostart.Checked; Fire(); } };
            _startMin.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.StartMinimized = _startMin.Checked; Fire(); } };
            _tray.CheckedChanged += (o, e) => { if (!Loading && S != null) { S.MinimizeToTray = _tray.Checked; Fire(); } };

            _hotkey.CheckedChanged += (o, e) =>
            {
                if (!_hotkey.Checked) _comboRow.Close();
                UpdateVisibility();
                if (Loading || S == null) return;
                S.HotkeyEnabled = _hotkey.Checked;
                Fire();
            };
            _comboRow.Opening += FillHotkey;
            foreach (ChipBox chip in new[] { _ctrl, _alt, _shift, _win })
                chip.CheckedChanged += (o, e) => UpdateHotkeyProblem();
            _key.SelectedIndexChanged += (o, e) => UpdateHotkeyProblem();
            _comboRow.SaveRequested += SaveHotkey;

            _log.CheckedChanged += (o, e) =>
            {
                if (!_log.Checked) _fileRow.Close();
                UpdateVisibility();
                if (Loading || S == null) return;
                S.LogEnabled = _log.Checked;
                Fire();
            };
            _fileRow.Opening += () =>
            {
                if (S != null) _logPath.Text = CurrentLogPath();
            };
            _browse.Click += (o, e) => BrowseLog();
            _useDefault.Click += (o, e) => _logPath.Text = DefaultLogPath();
            _fileRow.SaveRequested += SaveLogPath;
            _openLog.Click += (o, e) => { if (OpenLogRequested != null) OpenLogRequested(); };

            _langRow.Opening += () =>
            {
                if (S != null) _lang.SelectedIndex = S.Language == "de" ? 1 : (S.Language == "en" ? 2 : 0);
            };
            _lang.Committed += (o, e) => PickLanguage();
            _themeRow.Opening += () =>
            {
                if (S != null) _theme.SelectedIndex = S.Theme == "light" ? 1 : (S.Theme == "dark" ? 2 : 0);
            };
            _theme.Committed += (o, e) => PickTheme();
            _openFolder.Click += (o, e) => { if (OpenFolderRequested != null) OpenFolderRequested(); };
            _checkUpdate.Click += (o, e) => { if (OpenReleasePageRequested != null) OpenReleasePageRequested(); };
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _startOnLaunch.Checked = s.StartHoldingOnLaunch;
            _autostart.Checked = s.StartWithWindows;
            _startMin.Checked = s.StartMinimized;
            _tray.Checked = s.MinimizeToTray;
            _hotkey.Checked = s.HotkeyEnabled;
            _log.Checked = s.LogEnabled;
            ShowValues();
        }

        /// <summary>Ausgeschaltet zeigen Tastenkombination und Protokoll nur eine Zeile "Aus: ...".</summary>
        void UpdateVisibility()
        {
            _hotkeyOffRow.Visible = !_hotkey.Checked;
            _hotkeyBody.Visible = _hotkey.Checked;
            _logOffRow.Visible = !_log.Checked;
            _logBody.Visible = _log.Checked;
            RefreshLayout();
        }

        void ShowValues()
        {
            if (S == null) return;
            _comboRow.SetValue(Loc.T("sys.hotkey.combo"), Combination(S.HotkeyCtrl, S.HotkeyAlt, S.HotkeyShift, S.HotkeyWin, S.HotkeyKey));
            _fileRow.SetValue(Loc.T("sys.log.file"), FileName(CurrentLogPath()),
                string.IsNullOrEmpty(S.LogPath) ? Loc.T("sys.log.default") : "");

            bool autoLanguage = S.Language != "de" && S.Language != "en";
            string language = autoLanguage ? Loc.Language : S.Language;
            _langRow.SetValue(Loc.T("sys.language"), language == "en" ? "English" : "Deutsch", autoLanguage ? Loc.T("sys.auto.pill") : "");
            _themeRow.SetValue(Loc.T("sys.theme"), ThemeLabel(), S.Theme == "light" || S.Theme == "dark" ? "" : Loc.T("sys.auto.pill"));
            UpdateVisibility();
        }

        /// <summary>Die Darstellung, die gerade gilt: bei "automatisch" die von Windows (auch das Kontrastdesign).</summary>
        string ThemeLabel()
        {
            if (S.Theme == "light") return Loc.T("sys.theme.light");
            if (S.Theme == "dark") return Loc.T("sys.theme.dark");
            if (Theme.Mode == ThemeMode.HighContrast) return Loc.T("sys.theme.contrast");
            return Loc.T(Theme.IsDark ? "sys.theme.dark" : "sys.theme.light");
        }

        /// <summary>"Strg + Alt + G".</summary>
        static string Combination(bool ctrl, bool alt, bool shift, bool win, string key)
        {
            string text = "";
            if (ctrl) text += Loc.T("key.ctrl") + " + ";
            if (alt) text += Loc.T("key.alt") + " + ";
            if (shift) text += Loc.T("key.shift") + " + ";
            if (win) text += Loc.T("key.win") + " + ";
            return text + key;
        }

        string CurrentLogPath()
        {
            return S == null || string.IsNullOrEmpty(S.LogPath) ? DefaultLogPath() : S.LogPath;
        }

        static string FileName(string path)
        {
            try
            {
                string name = Path.GetFileName(path);
                return string.IsNullOrEmpty(name) ? path : name;
            }
            catch (ArgumentException)
            {
                return path;   // ungueltige Zeichen im Pfad: dann eben den ganzen Text zeigen
            }
        }

        // ------------------------------------------------------------------ Tastenkombination

        void FillHotkey()
        {
            if (S == null) return;
            _ctrl.Checked = S.HotkeyCtrl;
            _alt.Checked = S.HotkeyAlt;
            _shift.Checked = S.HotkeyShift;
            _win.Checked = S.HotkeyWin;
            int keyIndex = Array.IndexOf(Settings.HotkeyKeys, S.HotkeyKey);
            _key.SelectedIndex = keyIndex >= 0 ? keyIndex : Array.IndexOf(Settings.HotkeyKeys, "G");
            UpdateHotkeyProblem();
        }

        string SelectedKey
        {
            get { return _key.SelectedItem as string ?? "G"; }
        }

        /// <summary>Speichern geht nur mit mindestens einer Zusatztaste und ohne Konflikt mit einem Zeichen der Tastatur (AltGr).</summary>
        void UpdateHotkeyProblem()
        {
            _comboRow.Problem = HotkeyProblem();
        }

        string HotkeyProblem()
        {
            if (!_ctrl.Checked && !_alt.Checked && !_shift.Checked && !_win.Checked) return Loc.T("sys.hotkey.nomod");
            Func<Settings, string> typed = TypedCharacter;
            if (typed == null) return "";
            var probe = new Settings
            {
                HotkeyEnabled = true,
                HotkeyCtrl = _ctrl.Checked,
                HotkeyAlt = _alt.Checked,
                HotkeyShift = _shift.Checked,
                HotkeyWin = _win.Checked,
                HotkeyKey = SelectedKey,
            };
            string character = typed(probe);
            return character == null ? "" : Loc.T("sys.hotkey.conflict", HotkeyInfo.Describe(probe), character);
        }

        void SaveHotkey()
        {
            if (S == null) return;
            UpdateHotkeyProblem();
            if (_comboRow.Problem.Length > 0) return;
            S.HotkeyCtrl = _ctrl.Checked;
            S.HotkeyAlt = _alt.Checked;
            S.HotkeyShift = _shift.Checked;
            S.HotkeyWin = _win.Checked;
            S.HotkeyKey = SelectedKey;
            _comboRow.Close();
            ShowValues();
            Fire();
        }

        /// <summary>Meldet unter der Kombination, wenn sie nicht angemeldet werden konnte; klappt alles, steht dort nichts.</summary>
        /// <param name="character">Bei <see cref="HotkeyState.Conflict"/> das Zeichen, das getippt wuerde.</param>
        public void SetHotkeyStatus(HotkeyState state, string description, string character = null)
        {
            string text = "";
            if (state == HotkeyState.Failed) text = Loc.T("sys.hotkey.fail", description);
            else if (state == HotkeyState.Conflict) text = Loc.T("sys.hotkey.conflict", description, character ?? "?");
            _hotkeyStatus.Text = text;
            _hotkeyStatusRow.Visible = text.Length > 0;
            RefreshLayout();
        }

        // ------------------------------------------------------------------ Protokoll

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
                _logPath.Focus();
            }
        }

        void SaveLogPath()
        {
            if (S == null) return;
            string text = _logPath.Text.Trim();
            S.LogPath = text == DefaultLogPath() ? "" : text;
            _fileRow.Close();
            ShowValues();
            Fire();
        }

        /// <summary>
        /// Zeigt Speicher- und Protokollfehler dauerhaft an der jeweiligen Stelle (null = alles in Ordnung), damit ein nicht
        /// beschreibbarer Ordner nicht still Einstellungen verschluckt.
        /// </summary>
        public void SetStorageStatus(string settingsError, string logError)
        {
            _settingsError = settingsError;
            _logErrorText = logError;
            ApplyStorageTexts();
            RefreshLayout();
        }

        void ApplyStorageTexts()
        {
            _logError.Text = _logErrorText != null ? Loc.T("sys.log.fail", _logErrorText) : "";
            _logErrorRow.Visible = _logErrorText != null;

            if (_settingsError != null)
            {
                _storage.Text = Loc.T("sys.storage.fail", _settingsError);
                _storage.ForeColor = Theme.DangerText;
            }
            else
            {
                _storage.Text = IsPortable ? Loc.T("sys.portable") : "";
                _storage.ForeColor = Theme.Muted;
            }
            _storageRow.Visible = _storage.Text.Length > 0;
        }

        // ------------------------------------------------------------------ Sprache und Darstellung

        void PickLanguage()
        {
            if (S == null || _lang.SelectedIndex < 0) return;
            string language = _lang.SelectedIndex == 1 ? "de" : (_lang.SelectedIndex == 2 ? "en" : "auto");
            bool changed = S.Language != language;
            S.Language = language;
            _langRow.Close();
            ShowValues();
            if (changed) Fire();
        }

        void PickTheme()
        {
            if (S == null || _theme.SelectedIndex < 0) return;
            string theme = _theme.SelectedIndex == 1 ? "light" : (_theme.SelectedIndex == 2 ? "dark" : "auto");
            bool changed = S.Theme != theme;
            S.Theme = theme;
            _themeRow.Close();
            ShowValues();
            if (changed) Fire();
        }

        // ------------------------------------------------------------------ Texte

        public override void ApplyTexts()
        {
            _startCard.Text = Loc.T("sys.grp.start");
            _startOnLaunchRow.Title = Loc.T("sys.startonlaunch");
            _autostartRow.Title = Loc.T("sys.autostart");
            _autostartRow.Caption = Loc.T("sys.autostart.hint");
            _startMinRow.Title = Loc.T("sys.startmin");
            _trayRow.Title = Loc.T("sys.tray");

            _hotkeyHeader.Title = Loc.T("sys.grp.hotkey");
            _hotkeyHeader.Caption = Loc.T("sys.hotkey.caption");
            _hotkey.AccessibleName = Loc.T("sys.hotkey");   // nach Title: sonst hiesse der Schalter nur wie die Karte
            _hotkeyOff.Text = Loc.T("sys.hotkey.off");
            _comboRow.EditorTitle = Loc.T("sys.grp.hotkey");
            _comboRow.SetButtonTexts(Loc.T("btn.save"), Loc.T("btn.cancel"));
            _ctrl.Text = Loc.T("key.ctrl");
            _alt.Text = Loc.T("key.alt");
            _shift.Text = Loc.T("key.shift");
            _win.Text = Loc.T("key.win");
            _plus.Text = "+";
            _key.AccessibleName = Loc.T("sys.hotkey.key");

            _logHeader.Title = Loc.T("sys.grp.log");
            _logHeader.Caption = Loc.T("sys.log.caption");
            _log.AccessibleName = Loc.T("sys.log");
            _logOff.Text = Loc.T("sys.log.off");
            _fileRow.EditorTitle = Loc.T("sys.log.file.title");
            _fileRow.SetButtonTexts(Loc.T("btn.save"), Loc.T("btn.cancel"));
            _logPath.AccessibleName = Loc.T("sys.log.file.title");
            _browse.Text = Loc.T("sys.log.browse");
            _useDefault.Text = Loc.T("sys.log.usedefault");
            _logPathHint.Text = Loc.T("sys.log.hint");
            _openLog.Text = Loc.T("sys.log.openfile");

            _generalHeader.Title = Loc.T("sys.grp.general");
            _langRow.EditorTitle = Loc.T("sys.language");
            _lang.Items = new[] { Loc.T("sys.auto"), "Deutsch", "English" };
            _lang.AccessibleName = Loc.T("sys.language");
            _langHint.Text = Loc.T("sys.lang.hint");
            _themeRow.EditorTitle = Loc.T("sys.theme");
            _theme.Items = new[] { Loc.T("sys.auto"), Loc.T("sys.theme.light"), Loc.T("sys.theme.dark") };
            _theme.AccessibleName = Loc.T("sys.theme");
            _themeHint.Text = Loc.T("sys.theme.hint");
            _openFolder.Text = Loc.T("sys.folder.open");
            _checkUpdate.Text = Loc.T("sys.update.open");
            _updateHint.Text = Loc.T("sys.update.hint");
            ApplyStorageTexts();

            ShowValues();
            RefreshLayout();
        }
    }
}
