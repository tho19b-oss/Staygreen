using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using StayGreen.Core;
using StayGreen.Platform;

namespace StayGreen.UI
{
    /// <summary>
    /// Verdrahtet alles: Einstellungen, Engine, Takt, Auto-Stopp, Tray-Symbol, Hotkey, Befehlskanal, Hauptfenster.
    /// Laeuft als ApplicationContext, damit die Anwendung auch ohne sichtbares Fenster (nur Infobereich) lebt.
    /// Die Logik steckt in der Kernschicht (Engine, AutoStopCoordinator, Planer); hier wird sie nur mit Windows verbunden.
    /// </summary>
    sealed class AppController : ApplicationContext
    {
        /// <summary>Ein Ereignis aus einem anderen Thread (Sitzung, Energie), das im Takt protokolliert wird.</summary>
        sealed class PendingEvent
        {
            public DateTime Time;
            public string Kind;
            public string MessageKey;
        }

        /// <summary>Zeigt die Vorwarnung vor dem Auto-Stopp als Dialog.</summary>
        sealed class WarningPrompt : IAutoStopPrompt
        {
            readonly Settings _settings;

            public WarningPrompt(Settings settings)
            {
                _settings = settings;
            }

            public AutoStopChoice Ask(DateTime due, string actions)
            {
                using (var dialog = new CountdownForm(due, () => DateTime.Now, actions, _settings.AutoStopSnoozeMinutes))
                {
                    dialog.ShowDialog();
                    return dialog.Choice;
                }
            }
        }

        readonly CommandLine _cmd;
        readonly SingleInstance _instance;
        readonly Settings _settings;
        readonly IInputBackend _input;
        readonly WindowsSystemActions _actions = new WindowsSystemActions();
        readonly HolderEngine _engine;
        readonly ProtocolLog _log;
        readonly TrayController _tray;
        readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly HotkeyWindow _hotkey = new HotkeyWindow();
        readonly AutoStopCoordinator _autoStop;
        readonly CommandPipe _pipe = new CommandPipe();
        readonly WheelGuard _wheelGuard = new WheelGuard();
        readonly List<PendingEvent> _pending = new List<PendingEvent>();

        MainForm _form;
        SynchronizationContext _ui;
        bool _exiting;
        bool _balloonShown;
        bool _noticePending;
        volatile bool _sessionLocked;
        string _appliedTheme;
        string _hotkeySignature;
        HotkeyState _hotkeyState = HotkeyState.Off;
        string _hotkeyDescription = "";
        string _hotkeyCharacter;
        string _storageError;
        string _shownLogError;

        public AppController(CommandLine cmd, SingleInstance instance)
        {
            _cmd = cmd;
            _instance = instance;

            _settings = SettingsStore.Load();
            ApplyLanguage();
            _appliedTheme = _cmd.Theme ?? _settings.Theme;
            Theme.Apply(_appliedTheme);

            _input = PlatformInfo.IsWindows ? (IInputBackend)new Win32Input() : new DemoInput();
            _log = new ProtocolLog(_settings, () => SettingsStore.DefaultLogPath);
            _engine = new HolderEngine(_input, _settings, (time, kind, message) => _log.Write(time, kind, message), _actions);
            _autoStop = new AutoStopCoordinator(_settings, new WarningPrompt(_settings), () => DateTime.Now);

            WindowIcon = IconFactory.Create(Theme.Green, Glyph.Check, 32);
            Application.AddMessageFilter(_wheelGuard); // Mausrad ueber einem Feld scrollt die Seite, statt Werte zu aendern

            // Der Autostart-Eintrag in der Registry ist die Wahrheit (der Nutzer kann ihn auch ausserhalb aendern).
            _settings.StartWithWindows = Autostart.IsEnabled();
            if (_settings.StartWithWindows && Autostart.CurrentCommand() != Autostart.ExpectedCommand)
                Autostart.Set(true); // EXE wurde verschoben: Eintrag auf den neuen Pfad umbiegen

            _form = CreateForm(_cmd.Tab);
            _ui = SynchronizationContext.Current;   // steht erst nach dem ersten Fenster bereit

            _tray = new TrayController();
            _tray.ToggleRequested += () => Toggle(Loc.T("reason.tray"));
            _tray.ShowRequested += ShowMainWindow;
            _tray.ResumeRequested += ResumeFromPause;
            _tray.ExitRequested += ExitApp;
            _tray.PauseRequested += minutes => PauseFor(minutes, Loc.T("reason.tray"));
            _tray.AutostartRequested += enabled =>
            {
                _settings.StartWithWindows = enabled;
                OnSettingsChanged();
                _form.LoadSettings(_settings);
            };

            _hotkey.Pressed += () => Toggle(Loc.T("reason.hotkey"));
            ApplyHotkey();

            HookSystemEvents();
            _instance.Listen(() => Post(ShowMainWindow));
            _pipe.Listen(OnRemoteLine);

            _timer.Tick += (o, e) => OnTick();

            DateTime now = DateTime.Now;
            _autoStop.Reschedule(now);

            bool start = _cmd.StartRequested || (_settings.StartHoldingOnLaunch && !_cmd.NoStartRequested);
            if (start) _engine.Start(now, Loc.T(_cmd.Autostart ? "reason.autostart" : "reason.launch"));
            if (_cmd.Remote != null && _cmd.Remote.Action == RemoteAction.Pause && _engine.Running)
                _engine.Pause(now, TimeSpan.FromMinutes(_cmd.Remote.Minutes), Loc.T("reason.command"));

            bool quiet = _cmd.Autostart || _cmd.Minimized || _settings.StartMinimized;
            if (!quiet)
            {
                ShowMainWindow();
            }
            else if (!_settings.MinimizeToTray)
            {
                _form.WindowState = FormWindowState.Minimized;
                _form.Show();
            }

            RefreshUi(now);
            _timer.Start();
        }

        public Icon WindowIcon { get; private set; }

        public bool IsExiting
        {
            get { return _exiting; }
        }

        public HolderEngine Engine
        {
            get { return _engine; }
        }

        /// <summary>Fuehrt eine Aktion auf dem Oberflaechen-Thread aus (auch von anderen Threads aus aufrufbar).</summary>
        void Post(Action action)
        {
            SynchronizationContext ui = _ui;
            if (ui == null) return;
            ui.Post(state =>
            {
                if (_exiting) return;
                try { action(); }
                catch (Exception ex) { CrashLog.Write(ex); }
            }, null);
        }

        // ------------------------------------------------------------------ Fenster aufbauen

        MainForm CreateForm(int tab)
        {
            var form = new MainForm(this);
            form.ActivityPage.TestRequested = TestInput;
            form.SystemPage.DefaultLogPath = () => SettingsStore.DefaultLogPath;
            form.SystemPage.TypedCharacter = KeyboardLayouts.TypedCharacter;
            form.SystemPage.OpenFolderRequested = OpenSettingsFolder;
            form.SystemPage.OpenLogRequested = OpenLogFile;
            form.SystemPage.OpenReleasePageRequested = OpenReleasePage;
            form.LoadSettings(_settings);
            form.SelectTab(tab);
            IntPtr unused = form.Handle; // Handle jetzt anlegen, damit Signale der zweiten Instanz und Post() ankommen
            return form;
        }

        /// <summary>
        /// Baut das Hauptfenster neu auf (nach einem Wechsel der Darstellung): Viele Bedienelemente uebernehmen ihre Farben
        /// beim Anlegen, ein Neuaufbau ist der zuverlaessigste Weg. Position, Groesse und Seite bleiben erhalten.
        /// </summary>
        void RebuildForm()
        {
            if (_exiting) return;

            MainForm old = _form;
            bool visible = old.Visible;
            Rectangle bounds = old.WindowState == FormWindowState.Normal ? old.Bounds : old.RestoreBounds;
            int tab = old.SelectedTab;

            Theme.Apply(_appliedTheme);
            _form = CreateForm(tab);
            if (visible)
            {
                _form.StartPosition = FormStartPosition.Manual;
                _form.Bounds = bounds;
                ShowMainWindow();
            }

            old.Hide();
            old.Dispose();
            PushStatuses();
            RefreshUi(DateTime.Now);
        }

        /// <summary>Wechselt die Darstellung, wenn Einstellung oder Windows (Hell/Dunkel, Kontrastdesign) es verlangen.</summary>
        void ApplyThemeIfChanged()
        {
            string wanted = _cmd.Theme ?? _settings.Theme;
            if (wanted == _appliedTheme && Theme.Resolve(_appliedTheme) == Theme.Mode) return;
            _appliedTheme = wanted;
            Post(RebuildForm);
        }

        // ------------------------------------------------------------------ Start / Stopp / Pause

        public void Start(string reason)
        {
            _engine.Start(DateTime.Now, reason);
            RefreshUi(DateTime.Now);
        }

        public void Stop(string reason)
        {
            _engine.Stop(DateTime.Now, reason);
            RefreshUi(DateTime.Now);
        }

        public void Toggle(string reason)
        {
            if (_engine.Running) Stop(reason);
            else Start(reason);
        }

        /// <summary>Haelt das Aktivhalten fuer ein paar Minuten an; danach geht es von selbst weiter.</summary>
        public void PauseFor(int minutes, string reason)
        {
            if (!_engine.Running) return;
            _engine.Pause(DateTime.Now, TimeSpan.FromMinutes(minutes), reason);
            RefreshUi(DateTime.Now);
        }

        public void ResumeFromPause()
        {
            _engine.Resume(DateTime.Now);
            RefreshUi(DateTime.Now);
        }

        /// <summary>"Eingabe testen": blockiert kurz (gut 300 ms), bis klar ist, ob Windows die Eingabe als Aktivitaet wertet.</summary>
        InputTestResult TestInput()
        {
            Stopwatch clock = Stopwatch.StartNew();
            return InputTest.Run(_input, _settings, () => clock.Elapsed, Thread.Sleep);
        }

        // ------------------------------------------------------------------ Befehle von einem zweiten Start

        /// <summary>Wird auf dem Hintergrundthread des Befehlskanals aufgerufen.</summary>
        void OnRemoteLine(string line)
        {
            RemoteCommand command;
            if (!RemoteCommand.TryParse(line, out command)) return;
            Post(() => ApplyRemote(command));
        }

        void ApplyRemote(RemoteCommand command)
        {
            string reason = Loc.T("reason.command");
            switch (command.Action)
            {
                case RemoteAction.Start:
                    if (!_engine.Running) Start(reason);
                    break;
                case RemoteAction.Stop:
                    if (_engine.Running) Stop(reason);
                    break;
                case RemoteAction.Toggle:
                    Toggle(reason);
                    break;
                case RemoteAction.Pause:
                    PauseFor(command.Minutes, reason);
                    break;
                case RemoteAction.Resume:
                    ResumeFromPause();
                    break;
                default:
                    ShowMainWindow();
                    break;
            }
        }

        // ------------------------------------------------------------------ Takt

        void OnTick()
        {
            if (_exiting) return;
            DateTime now = DateTime.Now;
            try
            {
                DrainPendingEvents();
                _engine.Tick(now);
                CheckAutoStop(now);
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
            RefreshUi(now);
        }

        // ------------------------------------------------------------------ Auto-Stopp

        void CheckAutoStop(DateTime now)
        {
            AutoStopResult result = _autoStop.Tick(now);
            if (_autoStop.SettingsChanged)
            {
                SaveSettings();
                _form.LoadSettings(_settings);
            }

            DateTime after = DateTime.Now;
            switch (result)
            {
                case AutoStopResult.Execute:
                    ExecuteAutoStop(after);
                    break;
                case AutoStopResult.Missed:
                    _log.Write(after, "AUTOSTOP", Loc.T("log.autostop.missed"));
                    break;
                case AutoStopResult.Cancelled:
                    _log.Write(after, "AUTOSTOP", Loc.T("log.autostop.cancelled"));
                    break;
                case AutoStopResult.Snoozed:
                    if (_autoStop.LastSnoozedTo.HasValue)
                        _log.Write(after, "AUTOSTOP", Loc.T("log.autostop.snoozed", _settings.AutoStopSnoozeMinutes,
                            StatusBuilder.When(_autoStop.LastSnoozedTo.Value, after)));
                    break;
            }
        }

        void ExecuteAutoStop(DateTime now)
        {
            StopPlan plan = StopPlan.FromSettings(_settings);
            _log.Write(now, "AUTOSTOP", Loc.T("log.autostop", plan.Describe()));
            if (_engine.Running) _engine.Stop(now, Loc.T("reason.autostop"));
            RefreshUi(now);

            StopOutcome outcome = StopExecutor.Execute(plan, _actions);

            DateTime after = DateTime.Now;
            if (outcome.TeamsClosed) _log.Write(after, "AUTOSTOP", Loc.T("log.teams.closed"));
            if (outcome.ShutdownStarted) _log.Write(after, "AUTOSTOP", Loc.T("log.shutdown"));
            if (outcome.Locked) _log.Write(after, "AUTOSTOP", Loc.T("log.locked"));
            if (outcome.ExitRequested)
            {
                _log.Write(after, "EXIT", Loc.T("log.exit"));
                ExitApp();
            }
        }

        // ------------------------------------------------------------------ Einstellungen

        /// <summary>Wird von den Registerkarten aufgerufen, sobald der Nutzer etwas aendert.</summary>
        public void OnSettingsChanged()
        {
            string before = _settings.Serialize();
            _settings.Normalize();
            bool adjusted = before != _settings.Serialize();

            string oldLanguage = Loc.Language;
            ApplyLanguage();
            if (Loc.Language != oldLanguage)
            {
                _form.ApplyTexts();
                _tray.ApplyTexts();
                PushStatuses();
            }

            ApplyAutostart();
            ApplyHotkey();
            _autoStop.Reschedule(DateTime.Now);
            SaveSettings();
            _log.Probe();   // Ein neu gewaehlter Protokollpfad zeigt sofort, ob er beschreibbar ist.

            // Hat die Pruefung einen Wert korrigiert (z. B. Hotkey ohne Modifier), Felder nachziehen.
            if (adjusted) _form.LoadSettings(_settings);
            ApplyThemeIfChanged();
            RefreshUi(DateTime.Now);
        }

        /// <summary>Speichert die Einstellungen; ein Fehler wird einmal als Hinweis gezeigt und bleibt in der Systemseite sichtbar.</summary>
        void SaveSettings()
        {
            bool ok = SettingsStore.Save(_settings);
            string error = ok ? null : (SettingsStore.LastError ?? "?");
            if (error == _storageError) return;

            _storageError = error;
            if (error != null)
                _tray.ShowBalloon(Loc.T("app.title"), Loc.T("balloon.storage", error), ToolTipIcon.Warning);
            PushStorageStatus();
        }

        void ApplyLanguage()
        {
            string setting = _cmd.Language ?? _settings.Language;
            Loc.Language = Loc.Resolve(setting, CultureInfo.CurrentUICulture);
        }

        void ApplyAutostart()
        {
            if (_settings.StartWithWindows == Autostart.IsEnabled()) return;
            if (Autostart.Set(_settings.StartWithWindows)) return;

            // Blockiert (Richtlinie, Sicherheitssoftware): Schalter zuruecksetzen und erklaeren.
            _settings.StartWithWindows = Autostart.IsEnabled();
            _form.LoadSettings(_settings);
            MessageBox.Show(_form, Loc.T("sys.autostart.fail"), Loc.T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void ApplyHotkey()
        {
            string signature = string.Join("|", new[]
            {
                _settings.HotkeyEnabled.ToString(), _settings.HotkeyCtrl.ToString(), _settings.HotkeyAlt.ToString(),
                _settings.HotkeyShift.ToString(), _settings.HotkeyWin.ToString(), _settings.HotkeyKey,
            });
            if (signature == _hotkeySignature) return;
            _hotkeySignature = signature;

            _hotkeyDescription = HotkeyInfo.Describe(_settings);
            _hotkeyCharacter = null;

            if (!_settings.HotkeyEnabled || !PlatformInfo.IsWindows)
            {
                _hotkey.Unregister();
                _hotkeyState = HotkeyState.Off;
            }
            else
            {
                // Strg+Alt ist auf Tastaturen mit AltGr dasselbe wie AltGr: Gibt die Kombination dort ein Zeichen ein,
                // wuerde der Hotkey es abfangen (z. B. "@" auf Strg+Alt+Q). Dann lieber gar nicht anmelden.
                string typed = KeyboardLayouts.TypedCharacter(_settings);
                if (typed != null)
                {
                    _hotkey.Unregister();
                    _hotkeyState = HotkeyState.Conflict;
                    _hotkeyCharacter = typed;
                }
                else
                {
                    _hotkeyState = _hotkey.Register(_settings) ? HotkeyState.Active : HotkeyState.Failed;
                }
            }
            PushHotkeyStatus();
        }

        void PushHotkeyStatus()
        {
            _form.SystemPage.SetHotkeyStatus(_hotkeyState, _hotkeyDescription, _hotkeyCharacter);
        }

        void PushStorageStatus()
        {
            _form.SystemPage.SetStorageStatus(_storageError, _shownLogError);
        }

        void PushStatuses()
        {
            PushHotkeyStatus();
            PushStorageStatus();
        }

        // ------------------------------------------------------------------ Anzeige

        void RefreshUi(DateTime now)
        {
            if (_exiting) return;
            try
            {
                CheckLogError();

                StatusInfo status = StatusBuilder.Build(_engine, _settings, now, _sessionLocked);
                string plan = StatusBuilder.PlanLine(_settings, now, _autoStop.Due, status.Kind != StatusKind.WaitingForWindow);

                if (_form.Visible)
                    _form.UpdateStatus(status, plan, _engine.Running, now, _autoStop.Due);

                _tray.Update(status, new TrayState
                {
                    Running = _engine.Running,
                    Paused = _engine.State == HolderState.Paused,
                    Autostart = _settings.StartWithWindows,
                });
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        }

        /// <summary>Ein nicht beschreibbares Protokoll wird einmal gemeldet und dauerhaft an der Einstellung angezeigt.</summary>
        void CheckLogError()
        {
            string error = _settings.LogEnabled ? _log.LastError : null;
            if (error == _shownLogError) return;

            _shownLogError = error;
            if (error != null)
                _tray.ShowBalloon(Loc.T("app.title"), Loc.T("balloon.log", error), ToolTipIcon.Warning);
            PushStorageStatus();
        }

        // ------------------------------------------------------------------ Fenster

        public void ShowMainWindow()
        {
            if (_exiting) return;
            _form.ShowInTaskbar = true;
            if (!_form.Visible) _form.Show();
            if (_form.WindowState == FormWindowState.Minimized) _form.WindowState = FormWindowState.Normal;
            _form.TopMost = true;   // kurz nach vorn zwingen, damit es auch bei gesperrtem Vordergrund klappt
            _form.TopMost = false;
            _form.Activate();
            RefreshUi(DateTime.Now);
            ShowNoticeOnce();
        }

        /// <summary>Beim ersten Oeffnen des Fensters: Hinweis, dass der PC entsperrt bleibt und wo die Nutzung erlaubt sein muss.</summary>
        void ShowNoticeOnce()
        {
            if (_settings.NoticeAccepted || _noticePending) return;
            _noticePending = true;
            Post(() =>
            {
                try
                {
                    MessageBox.Show(_form, Loc.T("notice.text"), Loc.T("notice.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _settings.NoticeAccepted = true;
                    SaveSettings();
                }
                finally
                {
                    _noticePending = false;
                }
            });
        }

        void HideToTray(bool showHint)
        {
            _form.Hide();
            _form.ShowInTaskbar = false;
            if (showHint && !_balloonShown)
            {
                _balloonShown = true;
                _tray.ShowBalloon(Loc.T("app.title"), Loc.T("balloon.tray"), ToolTipIcon.Info);
            }
        }

        /// <summary>Minimieren-Knopf: bei aktivierter Tray-Option ins Tray statt in die Taskleiste.</summary>
        public void OnMainWindowMinimized()
        {
            if (_settings.MinimizeToTray && !_exiting) HideToTray(true);
        }

        /// <summary>X-Knopf. True = Fenster darf schliessen (Programm endet), False = abbrechen.</summary>
        public bool OnMainWindowClosing()
        {
            if (_settings.MinimizeToTray)
            {
                HideToTray(true);
                return false;
            }

            if (_engine.Running)
            {
                DialogResult answer = MessageBox.Show(_form, Loc.T("confirm.exit"), Loc.T("app.title"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) return false;
            }
            ExitApp();
            return true;
        }

        public void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;
            try
            {
                _timer.Stop();
                if (_engine.Running) _engine.Stop(DateTime.Now, Loc.T("reason.exit"));
                UnhookSystemEvents();
                _pipe.Dispose();
                _hotkey.Dispose();
                _tray.Dispose();
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
            ExitThread();
        }

        // ------------------------------------------------------------------ Hilfen

        void OpenSettingsFolder()
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.Directory);
                Process.Start(new ProcessStartInfo(SettingsStore.Directory) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        }

        void OpenLogFile()
        {
            try
            {
                string path = _log.CurrentPath;
                if (!File.Exists(path))
                {
                    MessageBox.Show(_form, Loc.T("sys.log.nofile", path), Loc.T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        }

        /// <summary>Oeffnet die Release-Seite im Browser des Nutzers. StayGreen selbst baut keine Verbindung auf.</summary>
        void OpenReleasePage()
        {
            try
            {
                Process.Start(new ProcessStartInfo(AppInfo.ReleaseUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        }

        // ------------------------------------------------------------------ Sitzung und Energie (laufen auf eigenen Threads)

        void HookSystemEvents()
        {
            try
            {
                SystemEvents.SessionSwitch += OnSessionSwitch;
                SystemEvents.PowerModeChanged += OnPowerModeChanged;
                SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            }
            catch { }
        }

        void UnhookSystemEvents()
        {
            try
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            }
            catch { }
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                    _sessionLocked = true;
                    Enqueue("LOCK", "log.session.lock");
                    break;
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                case SessionSwitchReason.SessionLogon:
                    _sessionLocked = false;
                    Enqueue("UNLOCK", "log.session.unlock");
                    break;
            }
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Suspend) Enqueue("SLEEP", "log.sleep");
            else if (e.Mode == PowerModes.Resume) Enqueue("WAKE", "log.wake");
        }

        /// <summary>Windows hat Hell/Dunkel oder das Kontrastdesign umgeschaltet: Bei "Automatisch" neu aufbauen.</summary>
        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color
                && e.Category != UserPreferenceCategory.Window)
                return;
            Post(() =>
            {
                if (Theme.Resolve(_appliedTheme) != Theme.Mode) RebuildForm();
            });
        }

        /// <summary>Merkt ein Ereignis vom System-Thread vor; protokolliert wird im Takt auf dem Oberflaechen-Thread.</summary>
        void Enqueue(string kind, string messageKey)
        {
            lock (_pending)
                _pending.Add(new PendingEvent { Time = DateTime.Now, Kind = kind, MessageKey = messageKey });
        }

        void DrainPendingEvents()
        {
            PendingEvent[] items;
            lock (_pending)
            {
                if (_pending.Count == 0) return;
                items = _pending.ToArray();
                _pending.Clear();
            }
            foreach (PendingEvent item in items) _log.Write(item.Time, item.Kind, Loc.T(item.MessageKey));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(_wheelGuard);
                _timer.Dispose();
                if (_form != null && !_form.IsDisposed) _form.Dispose();
                if (WindowIcon != null) WindowIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
