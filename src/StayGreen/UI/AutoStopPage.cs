using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Karte "Feierabend: Auto-Stopp": Kopf mit Titel, Erklaerung und Schalter, darunter drei Wertzeilen, die sich an Ort
    /// und Stelle zum Bearbeiten aufklappen: Wann (taeglich, einmalig oder am Zeitplanende), Dabei (Teams beenden, Sperren,
    /// Herunterfahren, StayGreen beenden) und Vorwarnung (nur, wenn eine Aktion einen Countdown zeigt). Ausgeschaltet bleibt
    /// eine Zeile "Aus: ...". Wann der naechste Auto-Stopp kommt, zeigt die Statuskarte; hier steht nur eine Kennzeichnung,
    /// wenn er so nie ausloesen kann.
    /// </summary>
    sealed class AutoStopPage : PageBase
    {
        readonly Card _card = new Card();
        readonly SwitchBox _enable = CreateSwitch();
        readonly SettingRow _header;
        readonly WrapLabel _offText = CreateHint();
        readonly Stack _offRow;
        readonly ListStack _list = new ListStack { TrailingDivider = false };

        readonly EditableRow _whenRow = new EditableRow(EditorKind.Input);
        readonly Segmented _when = new Segmented();
        readonly Label _dailyLabel = CreateEditorLabel();
        readonly TimeBox _dailyTime = new TimeBox { Compact = true };
        readonly FlowRow _dailyRow;
        readonly Label _dateLabel = CreateEditorLabel();
        readonly DateTimePicker _onceDate = CreateDatePicker();
        readonly Label _onceTimeLabel = CreateEditorLabel();
        readonly TimeBox _onceTime = new TimeBox { Compact = true };
        readonly FlowRow _onceRow;
        readonly WrapLabel _scheduleHint = CreateEditorHint();

        readonly EditableRow _actionsRow = new EditableRow(EditorKind.Input);
        readonly ChipBox _teams = new ChipBox();
        readonly ChipBox _lock = new ChipBox();
        readonly ChipBox _shutdown = new ChipBox();
        readonly ChipBox _exit = new ChipBox();
        readonly WrapLabel _actionsHint = CreateEditorHint();

        readonly EditableRow _warningRow = new EditableRow(EditorKind.Input);
        readonly Label _warnLabel = CreateEditorLabel();
        readonly NumberBox _warn = new NumberBox(Settings.MinWarnSeconds, Settings.MaxWarnSeconds);
        readonly Label _warnUnit = CreateEditorLabel();
        readonly Label _snoozeLabel = CreateEditorLabel();
        readonly NumberBox _snooze = new NumberBox(Settings.MinSnoozeMinutes, Settings.MaxSnoozeMinutes);
        readonly Label _snoozeUnit = CreateEditorLabel();
        readonly WrapLabel _warnHint = CreateEditorHint();

        /// <summary>Naechster Termin laut Statuszeile (null = keiner); erst nach dem ersten <see cref="Tick"/> bekannt.</summary>
        DateTime? _due;
        bool _ticked;

        public AutoStopPage()
        {
            // Kopf der Karte: Titel, Erklaerung und Schalter in einer Zeile (wie "Aktive Zeiten").
            _card.Text = "";
            _header = CreateHeader(_enable);
            _card.AddRow(_header);
            _offRow = Pad(_offText, 0, 2);
            _card.Add(_offRow);
            _card.Add(_list);

            _dailyRow = new FlowRow(_dailyLabel, _dailyTime);
            _onceRow = new FlowRow(_dateLabel, _onceDate, _onceTimeLabel, _onceTime);
            _whenRow.AddContent(_when);
            _whenRow.AddContent(_dailyRow);
            _whenRow.AddContent(_onceRow);
            _whenRow.AddContent(_scheduleHint);

            _actionsRow.WrapValue = true;
            _actionsRow.AddContent(new FlowRow(_teams, _lock, _shutdown, _exit));
            _actionsRow.AddContent(_actionsHint);

            _warningRow.AddContent(new FlowRow(_warnLabel, _warn, _warnUnit));
            _warningRow.AddContent(new FlowRow(_snoozeLabel, _snooze, _snoozeUnit));
            _warningRow.AddContent(_warnHint);

            foreach (EditableRow row in new[] { _whenRow, _actionsRow, _warningRow })
                _list.Controls.Add(Track(row));

            Controls.Add(_card);

            _enable.CheckedChanged += (o, e) =>
            {
                if (!_enable.Checked) CloseEditors(null);   // Ausschalten verwirft einen offenen Entwurf
                UpdateVisibility();
                if (Loading || S == null) return;
                S.AutoStopEnabled = _enable.Checked;
                Fire();
            };

            _whenRow.Opening += FillWhen;
            _when.SelectedIndexChanged += (o, e) => UpdateWhenEditor();
            _onceDate.ValueChanged += (o, e) => UpdateWhenProblem();
            _onceTime.ValueChanged += (o, e) => UpdateWhenProblem();
            _whenRow.SaveRequested += SaveWhen;

            _actionsRow.Opening += FillActions;
            foreach (ChipBox chip in new[] { _teams, _lock, _shutdown, _exit })
                chip.CheckedChanged += (o, e) => UpdateActionsHint();
            _actionsRow.SaveRequested += SaveActions;

            _warningRow.Opening += FillWarning;
            _warningRow.SaveRequested += SaveWarning;
        }

        protected override void Populate(Settings s)
        {
            ApplyTexts();
            _enable.Checked = s.AutoStopEnabled;
            ShowValues();
        }

        public override void Tick(DateTime now, DateTime? autoStopDue)
        {
            if (S == null || (_ticked && _due == autoStopDue)) return;
            _ticked = true;
            _due = autoStopDue;
            ShowValues();
        }

        // ------------------------------------------------------------------ Zeilen

        void ShowValues()
        {
            if (S == null) return;
            string problem = WhenProblem();
            _whenRow.SetValue(Loc.T("stop.when"), WhenLabel(), problem, problem.Length > 0);
            _actionsRow.SetValue(Loc.T("stop.actions"), ActionsLabel());
            _warningRow.SetValue(Loc.T("stop.warn"), Loc.T("stop.warn.value", S.AutoStopWarnSeconds, S.AutoStopSnoozeMinutes));
            UpdateVisibility();
        }

        /// <summary>Ausgeschaltet nur die Zeile "Aus: ..."; die Vorwarnung nur, wenn eine Aktion einen Countdown zeigt.</summary>
        void UpdateVisibility()
        {
            bool on = _enable.Checked;
            bool needsWarning = S != null && (S.StopCloseTeams || S.StopLock || S.StopShutdown);
            _offRow.Visible = !on;
            _list.Visible = on;
            if (!needsWarning) _warningRow.Close();
            _warningRow.Visible = needsWarning;
            RefreshLayout();
        }

        string WhenLabel()
        {
            switch (S.AutoStopTiming)
            {
                case StopTiming.Once:
                    if (S.AutoStopOnce == DateTime.MinValue) return Loc.T("stop.mode.once");
                    return Loc.T("stop.when.once", DateLabel(S.AutoStopOnce), ScheduleRule.FormatTime(S.AutoStopOnce.TimeOfDay));
                case StopTiming.ScheduleEnd:
                    return Loc.T("stop.mode.schedule");
                default:
                    return Loc.T("stop.when.daily", ScheduleRule.FormatTime(S.AutoStopTime));
            }
        }

        /// <summary>"Fr 09.10." (englisch "Fri Oct 9").</summary>
        static string DateLabel(DateTime date)
        {
            string day = Loc.DayShort(ScheduleRule.DayIndex(date.DayOfWeek));
            string text = Loc.Language == "en"
                ? date.ToString("MMM d", CultureInfo.InvariantCulture)
                : date.ToString("dd.MM.", CultureInfo.InvariantCulture);
            return day + " " + text;
        }

        /// <summary>Kennzeichnung an "Wann", wenn der Auto-Stopp so nie ausloest (dieselben Faelle wie in der Statuskarte).</summary>
        string WhenProblem()
        {
            if (S == null || !S.AutoStopEnabled || !_ticked || _due.HasValue) return "";
            if (S.AutoStopTiming == StopTiming.ScheduleEnd)
                return Loc.T(S.ScheduleActive ? "stop.pill.noend" : "stop.pill.noschedule");
            return Loc.T("stop.pill.past");
        }

        string ActionsLabel()
        {
            var parts = new List<string>();
            if (S.StopCloseTeams) parts.Add(Loc.T("stop.teams"));
            if (S.StopLock) parts.Add(Loc.T("stop.lock"));
            if (S.StopShutdown) parts.Add(Loc.T("stop.shutdown"));
            if (S.StopExitApp) parts.Add(Loc.T("stop.exit"));
            return parts.Count > 0 ? string.Join(", ", parts) : Loc.T("stop.actions.none");
        }

        // ------------------------------------------------------------------ Wann

        void FillWhen()
        {
            if (S == null) return;
            _when.SelectedIndex = (int)S.AutoStopTiming;
            _dailyTime.Value = S.AutoStopTime;
            DateTime once = S.AutoStopOnce == DateTime.MinValue ? SuggestOnce() : S.AutoStopOnce;
            once = once < _onceDate.MinDate ? _onceDate.MinDate : (once > _onceDate.MaxDate ? _onceDate.MaxDate : once);
            _onceDate.Value = once;
            _onceTime.Value = once.TimeOfDay;
            UpdateWhenEditor();
        }

        /// <summary>Sinnvoller Vorschlag fuer "einmalig": heute 17:00, sonst morgen 17:00.</summary>
        static DateTime SuggestOnce()
        {
            DateTime t = DateTime.Today.AddHours(17);
            return t > DateTime.Now ? t : t.AddDays(1);
        }

        StopTiming SelectedTiming
        {
            get { return (StopTiming)Math.Max(0, _when.SelectedIndex); }
        }

        /// <summary>Zeigt zur gewaehlten Art die passenden Felder: Uhrzeit, Datum und Uhrzeit oder den Hinweis zum Zeitplanende.</summary>
        void UpdateWhenEditor()
        {
            StopTiming timing = SelectedTiming;
            _dailyRow.Visible = timing == StopTiming.Daily;
            _onceRow.Visible = timing == StopTiming.Once;
            _scheduleHint.Visible = timing == StopTiming.ScheduleEnd;
            UpdateWhenProblem();
            RefreshLayout();
        }

        /// <summary>Ein einmaliger Termin in der Vergangenheit loest nie aus: Speichern bleibt dann gesperrt.</summary>
        void UpdateWhenProblem()
        {
            bool past = SelectedTiming == StopTiming.Once && _onceDate.Value.Date + _onceTime.Value <= DateTime.Now;
            _whenRow.Problem = past ? Loc.T("stop.err.past") : "";
        }

        void SaveWhen()
        {
            if (S == null) return;
            UpdateWhenProblem();
            if (_whenRow.Problem.Length > 0) return;
            S.AutoStopTiming = SelectedTiming;
            S.AutoStopTime = _dailyTime.Value;
            if (S.AutoStopTiming == StopTiming.Once) S.AutoStopOnce = _onceDate.Value.Date + _onceTime.Value;
            _whenRow.Close();
            ShowValues();
            Fire();
        }

        // ------------------------------------------------------------------ Dabei

        void FillActions()
        {
            if (S == null) return;
            _teams.Checked = S.StopCloseTeams;
            _lock.Checked = S.StopLock;
            _shutdown.Checked = S.StopShutdown;
            _exit.Checked = S.StopExitApp;
            UpdateActionsHint();
        }

        /// <summary>Erklaert nur die gewaehlten Aktionen, die etwas Bleibendes bewirken.</summary>
        void UpdateActionsHint()
        {
            var lines = new List<string>();
            if (_teams.Checked) lines.Add(Loc.T("stop.teams") + ": " + Loc.T("stop.teams.hint"));
            if (_shutdown.Checked) lines.Add(Loc.T("stop.shutdown") + ": " + Loc.T("stop.shutdown.hint"));
            if (_exit.Checked) lines.Add(Loc.T("stop.exit") + ": " + Loc.T("stop.exit.hint"));
            if (!_teams.Checked && !_lock.Checked && !_shutdown.Checked && !_exit.Checked) lines.Add(Loc.T("stop.actions.nonehint"));
            _actionsHint.Text = string.Join("\n", lines);
            _actionsHint.Visible = lines.Count > 0;
            RefreshLayout();
        }

        void SaveActions()
        {
            if (S == null) return;
            S.StopCloseTeams = _teams.Checked;
            S.StopLock = _lock.Checked;
            S.StopShutdown = _shutdown.Checked;
            S.StopExitApp = _exit.Checked;
            _actionsRow.Close();
            ShowValues();
            Fire();
        }

        // ------------------------------------------------------------------ Vorwarnung

        void FillWarning()
        {
            if (S == null) return;
            _warn.Value = S.AutoStopWarnSeconds;
            _snooze.Value = S.AutoStopSnoozeMinutes;
        }

        void SaveWarning()
        {
            if (S == null) return;
            S.AutoStopWarnSeconds = _warn.Value;
            S.AutoStopSnoozeMinutes = _snooze.Value;
            _warningRow.Close();
            ShowValues();
            Fire();
        }

        // ------------------------------------------------------------------ Texte

        public override void ApplyTexts()
        {
            _header.Title = Loc.T("stop.grp.title");
            _header.Caption = Loc.T("stop.intro");
            _enable.AccessibleName = Loc.T("stop.enable");   // nach Title: die Zeile benennt den Schalter sonst nur nach der Karte
            _offText.Text = Loc.T("stop.off");

            _whenRow.EditorTitle = Loc.T("stop.when.title");
            _whenRow.SetButtonTexts(Loc.T("btn.save"), Loc.T("btn.cancel"));
            _when.Items = new[] { Loc.T("stop.mode.daily"), Loc.T("stop.mode.once"), Loc.T("stop.mode.schedule") };
            _when.AccessibleName = Loc.T("stop.when");
            _dailyLabel.Text = Loc.T("stop.time");
            _dailyTime.AccessibleName = Loc.T("stop.time");
            _dateLabel.Text = Loc.T("stop.date");
            _onceDate.AccessibleName = Loc.T("stop.date");
            _onceTimeLabel.Text = Loc.T("stop.time");
            _onceTime.AccessibleName = Loc.T("stop.time");
            _scheduleHint.Text = Loc.T("stop.schedule.hint");

            _actionsRow.EditorTitle = Loc.T("stop.grp.actions");
            _actionsRow.SetButtonTexts(Loc.T("btn.save"), Loc.T("btn.cancel"));
            _teams.Text = Loc.T("stop.teams");
            _lock.Text = Loc.T("stop.lock");
            _shutdown.Text = Loc.T("stop.shutdown");
            _exit.Text = Loc.T("stop.exit");
            UpdateActionsHint();

            _warningRow.EditorTitle = Loc.T("stop.warn");
            _warningRow.SetButtonTexts(Loc.T("btn.save"), Loc.T("btn.cancel"));
            _warnLabel.Text = Loc.T("stop.warn.countdown");
            _warn.AccessibleName = Loc.T("stop.warn.countdown");
            _warnUnit.Text = Loc.T("stop.warn.unit");
            _snoozeLabel.Text = Loc.T("stop.snooze");
            _snooze.AccessibleName = Loc.T("stop.snooze");
            _snoozeUnit.Text = Loc.T("stop.snooze.unit");
            AlignLabels(_warnLabel, _snoozeLabel);
            _warnHint.Text = Loc.T("stop.warn.hint");

            ShowValues();
            RefreshLayout();
        }

        /// <summary>Gleich breite Beschriftungen, damit die Zahlenfelder untereinander stehen.</summary>
        static void AlignLabels(params Label[] labels)
        {
            var size = Size.Empty;
            foreach (Label label in labels)
            {
                label.AutoSize = true;
                Size preferred = label.GetPreferredSize(Size.Empty);
                size = new Size(Math.Max(size.Width, preferred.Width), Math.Max(size.Height, preferred.Height));
            }
            foreach (Label label in labels)
            {
                label.AutoSize = false;
                label.Size = size;
            }
        }
    }
}
