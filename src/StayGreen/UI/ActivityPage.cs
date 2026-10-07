using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Karte "So bleibst du gruen": Methode, Taste, Intervall und Mausweg als Wertzeilen, die sich an Ort und Stelle zum
    /// Bearbeiten aufklappen (Methode und Taste gelten sofort, die Zahlen mit Speichern), darunter der Eingabetest.
    /// Karte "Verhalten": intelligenter Modus, Wach-Halten und Teams-Pruefung als Schalter.
    /// </summary>
    sealed class ActivityPage : PageBase
    {
        readonly Card _methodCard = new Card();
        readonly SettingRow _methodHeader = CreateHeader(null);
        readonly ListStack _list = new ListStack();

        readonly EditableRow _modeRow = new EditableRow(EditorKind.Choice);
        readonly Segmented _mode = new Segmented();
        readonly WrapLabel _modeHint = CreateEditorHint();

        readonly EditableRow _keyRow = new EditableRow(EditorKind.Choice);
        readonly ChoiceChips _key = new ChoiceChips();
        readonly WrapLabel _keyHint = CreateEditorHint();

        readonly EditableRow _intervalRow = new EditableRow(EditorKind.Input);
        readonly NumericUpDown _interval = Style(new NumericUpDown { Minimum = Settings.MinInterval, Maximum = Settings.MaxInterval });
        readonly Label _secondsUnit = CreateEditorLabel();
        readonly WrapLabel _intervalHint = CreateEditorHint();

        readonly EditableRow _pixelsRow = new EditableRow(EditorKind.Input);
        readonly NumericUpDown _pixels = Style(new NumericUpDown { Minimum = Settings.MinMousePixels, Maximum = Settings.MaxMousePixels });
        readonly Label _pixelsUnit = CreateEditorLabel();
        readonly WrapLabel _pixelsHint = CreateEditorHint();

        readonly FlatButton _test = CreateButton(ButtonKind.Secondary);
        readonly WrapLabel _testResult = new WrapLabel { Visible = false };
        readonly Stack _testRow = new Stack { Inset = new Padding(0, 10, 0, 2), Gap = 8 };
        readonly ToolTip _tips = new ToolTip();

        readonly Card _behaviorCard = new Card();
        readonly SwitchBox _smart = CreateSwitch();
        readonly SwitchBox _awake = CreateSwitch();
        readonly SwitchBox _teamsOnly = CreateSwitch();
        readonly SettingRow _smartRow;
        readonly SettingRow _awakeRow;
        readonly SettingRow _teamsRow;

        /// <summary>Wird vom Hauptfenster gesetzt: erzeugt eine Eingabe und meldet, ob Windows sie angenommen und als Aktivitaet gewertet hat.</summary>
        public Func<InputTestResult> TestRequested;

        public ActivityPage()
        {
            _interval.Width = Dpi.Px(76);
            _pixels.Width = Dpi.Px(76);

            // Kopf ohne Schalter: Titel und ein Satz, der erklaert, worum es geht.
            _methodCard.Text = "";
            _methodCard.AddRow(_methodHeader);
            _methodCard.Add(_list);

            _modeRow.AddContent(_mode);
            _modeRow.AddContent(_modeHint);
            _keyRow.AddContent(_key);
            _keyRow.AddContent(_keyHint);
            _intervalRow.AddContent(new FlowRow(_interval, _secondsUnit));
            _intervalRow.AddContent(_intervalHint);
            _pixelsRow.AddContent(new FlowRow(_pixels, _pixelsUnit));
            _pixelsRow.AddContent(_pixelsHint);
            foreach (EditableRow row in new[] { _modeRow, _keyRow, _intervalRow, _pixelsRow })
                _list.Controls.Add(Track(row));

            _testRow.Controls.Add(new FlowRow(_test));
            _testRow.Controls.Add(_testResult);
            _methodCard.Add(_testRow);

            _smartRow = new SettingRow(_smart);
            _awakeRow = new SettingRow(_awake);
            _teamsRow = new SettingRow(_teamsOnly);
            _behaviorCard.AddRow(_smartRow);
            _behaviorCard.AddRow(_awakeRow);
            _behaviorCard.AddRow(_teamsRow);

            Controls.Add(_methodCard);
            Controls.Add(_behaviorCard);

            _modeRow.Opening += () =>
            {
                if (S != null) _mode.SelectedIndex = (int)S.Mode;
            };
            _mode.Committed += (o, e) => PickMode();

            _keyRow.Opening += () =>
            {
                if (S != null) _key.SelectedIndex = (int)S.InputKey;
            };
            _key.Picked += (o, e) => PickKey();

            _intervalRow.Opening += () =>
            {
                if (S != null) _interval.Value = Clamp(S.IntervalSeconds, Settings.MinInterval, Settings.MaxInterval);
                UpdateIntervalHint();
            };
            _interval.TextChanged += (o, e) => UpdateIntervalHint();   // schon beim Tippen, nicht erst beim Verlassen
            _intervalRow.SaveRequested += SaveInterval;

            _pixelsRow.Opening += () =>
            {
                if (S != null) _pixels.Value = Clamp(S.MousePixels, Settings.MinMousePixels, Settings.MaxMousePixels);
            };
            _pixelsRow.SaveRequested += SavePixels;

            _test.Click += (o, e) =>
            {
                InputTestResult result = TestRequested != null ? TestRequested() : InputTestResult.Rejected;
                ShowTestResult(result, DateTime.Now);
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
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _smart.Checked = s.SmartIdle;
            _awake.Checked = s.KeepAwake;
            _teamsOnly.Checked = s.OnlyWhileTeamsRuns;
            ResetTestResult();
            ShowValues();
        }

        // ------------------------------------------------------------------ Werte

        /// <summary>Zeigt die Werte in den Zeilen. Taste und Mausweg erscheinen nur, wenn die Methode sie nutzt.</summary>
        void ShowValues()
        {
            if (S == null) return;
            bool risky = Settings.IsIntervalRisky(S.IntervalSeconds);
            _modeRow.SetValue(Loc.T("act.mode"), ModeLabel(S.Mode));
            _keyRow.SetValue(Loc.T("act.key"), KeyLabel(S.InputKey));
            _intervalRow.SetValue(Loc.T("act.interval"), Number(S.IntervalSeconds) + " " + Loc.T("act.unit.sec"),
                risky ? Loc.T("act.interval.risky") : "", risky);
            _pixelsRow.SetValue(Loc.T("act.pixels"), Number(S.MousePixels) + " " + Loc.T("act.unit.px"));
            ShowRow(_keyRow, S.Mode != ActivityMode.Mouse);
            ShowRow(_pixelsRow, S.Mode != ActivityMode.Key);
            RefreshLayout();
        }

        static void ShowRow(EditableRow row, bool visible)
        {
            if (!visible) row.Close();
            row.Visible = visible;
        }

        static string ModeLabel(ActivityMode mode)
        {
            switch (mode)
            {
                case ActivityMode.Key: return Loc.T("act.mode.key");
                case ActivityMode.Mouse: return Loc.T("act.mode.mouse");
                default: return Loc.T("act.mode.both");
            }
        }

        /// <summary>F13 bis F24 heissen ueberall gleich, die Umschalttaste je nach Sprache.</summary>
        static string KeyLabel(ActivityKey key)
        {
            return key == ActivityKey.Shift ? Loc.T("act.key.shift") : key.ToString();
        }

        static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        static decimal Clamp(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        void PickMode()
        {
            if (S == null || _mode.SelectedIndex < 0) return;
            var mode = (ActivityMode)_mode.SelectedIndex;
            bool changed = S.Mode != mode;
            S.Mode = mode;
            _modeRow.Close();
            ShowValues();
            if (!changed) return;
            ResetTestResult();
            Fire();
        }

        void PickKey()
        {
            if (S == null || _key.SelectedIndex < 0) return;
            var key = (ActivityKey)_key.SelectedIndex;
            bool changed = S.InputKey != key;
            S.InputKey = key;
            _keyRow.Close();
            ShowValues();
            if (!changed) return;
            ResetTestResult();
            Fire();
        }

        void SaveInterval()
        {
            if (S == null) return;
            S.IntervalSeconds = (int)_interval.Value;
            _intervalRow.Close();
            ShowValues();
            Fire();
        }

        void SavePixels()
        {
            if (S == null) return;
            S.MousePixels = (int)_pixels.Value;
            _pixelsRow.Close();
            ShowValues();
            ResetTestResult();
            Fire();
        }

        /// <summary>Teams wird nach etwa 5 Minuten abwesend: Ab 4 Minuten Abstand warnt der Hinweis im Editor in Rot.</summary>
        void UpdateIntervalHint()
        {
            // Den getippten Text lesen, nicht Value: Value wuerde eine halbe Eingabe schon pruefen und zurechtruecken.
            int seconds;
            if (!int.TryParse(_interval.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out seconds)) seconds = 0;
            bool risky = Settings.IsIntervalRisky(seconds);
            _intervalHint.Text = risky ? Loc.T("act.interval.warn", seconds) : Loc.T("act.hint");
            _intervalHint.ForeColor = risky ? Theme.DangerText : Theme.MutedStrong;
            RefreshLayout();
        }

        // ------------------------------------------------------------------ Eingabetest

        /// <summary>
        /// Zeigt das Ergebnis unter dem Knopf: gruen als Bestaetigung, sonst rot. Die Uhrzeit macht sichtbar, dass auch ein
        /// erneuter Klick wirklich neu getestet hat.
        /// </summary>
        void ShowTestResult(InputTestResult result, DateTime time)
        {
            string key = result == InputTestResult.Confirmed ? "act.test.ok"
                : result == InputTestResult.NotCounted ? "act.test.nocount" : "act.test.fail";
            _testResult.Text = Loc.T(key, time.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
            _testResult.ForeColor = result == InputTestResult.Confirmed ? Theme.GreenButton : Theme.DangerText;
            _testResult.Visible = true;
            RefreshLayout();
        }

        /// <summary>Ein frueheres Ergebnis gilt nicht mehr, sobald sich Methode, Taste oder Mausweg aendern.</summary>
        void ResetTestResult()
        {
            if (!_testResult.Visible && _testResult.Text.Length == 0) return;
            _testResult.Visible = false;
            _testResult.Text = "";
            RefreshLayout();
        }

        // ------------------------------------------------------------------ Texte

        public override void ApplyTexts()
        {
            _methodHeader.Title = Loc.T("act.grp.method");
            _methodHeader.Caption = Loc.T("act.method.caption");

            _modeRow.EditorTitle = Loc.T("act.mode");
            _mode.Items = new[] { Loc.T("act.mode.key"), Loc.T("act.mode.mouse"), Loc.T("act.mode.both.short") };
            _mode.AccessibleName = Loc.T("act.mode");
            _modeHint.Text = Loc.T("act.mode.hint");

            // Die Reihenfolge entspricht ActivityKey: F13 bis F24, dann die Umschalttaste.
            var keys = new List<string>();
            for (int i = 0; i <= (int)ActivityKey.Shift; i++) keys.Add(KeyLabel((ActivityKey)i));
            _keyRow.EditorTitle = Loc.T("act.key");
            _key.SetItems(keys);
            _key.AccessibleName = Loc.T("act.key");
            _keyHint.Text = Loc.T("act.key.hint");

            _intervalRow.EditorTitle = Loc.T("act.interval");
            _intervalRow.SetButtonTexts(Loc.T("btn.save"), Loc.T("btn.cancel"));
            _interval.AccessibleName = Loc.T("act.interval");
            _secondsUnit.Text = Loc.T("act.unit.sec");
            UpdateIntervalHint();

            _pixelsRow.EditorTitle = Loc.T("act.pixels");
            _pixelsRow.SetButtonTexts(Loc.T("btn.save"), Loc.T("btn.cancel"));
            _pixels.AccessibleName = Loc.T("act.pixels");
            _pixelsUnit.Text = Loc.T("act.unit.px");
            _pixelsHint.Text = Loc.T("act.pixels.hint");

            _test.Text = Loc.T("act.test");
            _test.AccessibleDescription = Loc.T("act.test.hint");
            _tips.SetToolTip(_test, Loc.T("act.test.hint"));

            _behaviorCard.Text = Loc.T("act.grp.behavior");
            _smartRow.Title = Loc.T("act.smart");
            _smartRow.Caption = Loc.T("act.smart.hint");
            _awakeRow.Title = Loc.T("act.awake");
            _awakeRow.Caption = Loc.T("act.awake.hint");
            _teamsRow.Title = Loc.T("act.teamsonly");
            _teamsRow.Caption = Loc.T("act.teamsonly.hint");

            ShowValues();
            RefreshLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tips.Dispose();
            base.Dispose(disposing);
        }
    }
}
