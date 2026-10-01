using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using StayGreen.Core;
using StayGreen.Platform;

namespace StayGreen.UI
{
    /// <summary>
    /// Verdrahtet alles: Einstellungen, Engine, Takt, Auto-Stopp, Tray-Symbol, Hotkey, Hauptfenster.
    /// Laeuft als ApplicationContext, damit die Anwendung auch ohne sichtbares Fenster (nur Infobereich) lebt.
    /// </summary>
    sealed class AppController : ApplicationContext
    {
        readonly CommandLine _cmd;
        readonly SingleInstance _instance;
        readonly Settings _settings;
        readonly IInputBackend _input;
        readonly ISystemActions _actions = new WindowsSystemActions();
        readonly HolderEngine _engine;
        readonly ProtocolLog _log;
        readonly MainForm _form;
        readonly NotifyIcon _tray;
        readonly Timer _timer = new Timer { Interval = 1000 };
        readonly HotkeyWindow _hotkey = new HotkeyWindow();
        readonly Dictionary<StatusKind, Icon> _icons = new Dictionary<StatusKind, Icon>();

        ToolStripMenuItem _miHeader;
        ToolStripMenuItem _miToggle;
        ToolStripMenuItem _miShow;
        ToolStripMenuItem _miAutostart;
        ToolStripMenuItem _miExit;

        DateTime? _autoStopDue;
        bool _executingStop;
        bool _exiting;
        bool _balloonShown;
        volatile bool _sessionLocked;
        StatusKind? _shownKind;
        string _shownTooltip;
        string _hotkeySignature;

        public AppController(CommandLine cmd, SingleInstance instance)
        {
            _cmd = cmd;
            _instance = instance;

            _settings = SettingsStore.Load();
            ApplyLanguage();

            _input = PlatformInfo.IsWindows ? (IInputBackend)new Win32Input() : new DemoInput();
            _log = new ProtocolLog(_settings, () => SettingsStore.DefaultLogPath);
            _engine = new HolderEngine(_input, _settings, (time, kind, message) => _log.Write(time, kind, message));

            CreateIcons();
            WindowIcon = IconFactory.Create(Theme.Green, Glyph.Check, 32);

            // Der Autostart-Eintrag in der Registry ist die Wahrheit (der Nutzer kann ihn auch ausserhalb aendern).
            _settings.StartWithWindows = Autostart.IsEnabled();
            if (_settings.StartWithWindows && Autostart.CurrentCommand() != Autostart.ExpectedCommand)
                Autostart.Set(true); // EXE wurde verschoben: Eintrag auf den neuen Pfad umbiegen

            _form = new MainForm(this);
            _form.ActivityPage.TestRequested = TestInput;
            _form.SystemPage.DefaultLogPath = () => SettingsStore.DefaultLogPath;
            _form.SystemPage.OpenFolderRequested = OpenSettingsFolder;
            _form.SystemPage.OpenLogRequested = OpenLogFile;
            _form.LoadSettings(_settings);
            _form.SelectTab(_cmd.Tab);
            IntPtr unused = _form.Handle; // Handle jetzt anlegen, damit Signale der zweiten Instanz ankommen

            _tray = BuildTray();

            _hotkey.Pressed += () => Toggle(Loc.T("reason.hotkey"));
            ApplyHotkey();

            HookSystemEvents();
            _instance.Listen(() =>
            {
                try { _form.BeginInvoke(new Action(ShowMainWindow)); }
                catch { }
            });

            _timer.Tick += (o, e) => OnTick();

            DateTime now = DateTime.Now;
            RescheduleAutoStop(now);
            bool start = _cmd.Start || (_settings.StartHoldingOnLaunch && !_cmd.NoStart);
            if (start) _engine.Start(now, Loc.T(_cmd.Autostart ? "reason.autostart" : "reason.launch"));

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

        // ------------------------------------------------------------------ Start / Stopp

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

        bool TestInput()
        {
            return _input.SendActivity(_settings.Mode, _settings.MousePixels);
        }

        // ------------------------------------------------------------------ Takt

        void OnTick()
        {
            if (_exiting) return;
            DateTime now = DateTime.Now;
            try
            {
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

        void RescheduleAutoStop(DateTime now)
        {
            _autoStopDue = AutoStopPlanner.NextDue(_settings, now);
        }

        void CheckAutoStop(DateTime now)
        {
            if (_executingStop || !_autoStopDue.HasValue || now < _autoStopDue.Value) return;

            DateTime due = _autoStopDue.Value;
            bool missed = AutoStopPlanner.IsMissed(due, now);

            // Naechsten Termin zuerst festlegen: der Countdown-Dialog pumpt Nachrichten, der Takt laeuft weiter.
            if (_settings.AutoStopTiming == StopTiming.Once)
            {
                _settings.AutoStopEnabled = false;
                SettingsStore.Save(_settings);
                _form.LoadSettings(_settings);
            }
            RescheduleAutoStop(now);

            if (missed)
            {
                _log.Write(now, "AUTOSTOP", Loc.T("log.autostop.missed"));
                return;
            }
            ExecuteAutoStop(now);
        }

        void ExecuteAutoStop(DateTime now)
        {
            _executingStop = true;
            try
            {
                StopPlan plan = StopPlan.FromSettings(_settings);
                _log.Write(now, "AUTOSTOP", Loc.T("log.autostop", DescribePlan(plan)));
                if (_engine.Running) _engine.Stop(now, Loc.T("reason.autostop"));
                RefreshUi(now);

                StopOutcome outcome = StopExecutor.Execute(plan, _actions, ConfirmShutdown);

                DateTime after = DateTime.Now;
                if (outcome.TeamsClosed) _log.Write(after, "AUTOSTOP", Loc.T("log.teams.closed"));
                if (outcome.ShutdownCancelled) _log.Write(after, "AUTOSTOP", Loc.T("log.shutdown.cancelled"));
                if (outcome.ShutdownStarted) _log.Write(after, "AUTOSTOP", Loc.T("log.shutdown"));
                if (outcome.Locked) _log.Write(after, "AUTOSTOP", Loc.T("log.locked"));
                if (outcome.ExitRequested)
                {
                    _log.Write(after, "EXIT", Loc.T("log.exit"));
                    ExitApp();
                }
            }
            finally
            {
                _executingStop = false;
            }
        }

        static string DescribePlan(StopPlan plan)
        {
            var parts = new List<string>();
            if (plan.CloseTeams) parts.Add(Loc.T("plan.action.teams"));
            if (plan.Lock) parts.Add(Loc.T("plan.action.lock"));
            if (plan.Shutdown) parts.Add(Loc.T("plan.action.shutdown"));
            if (plan.ExitApp) parts.Add(Loc.T("plan.action.exit"));
            return parts.Count == 0 ? Loc.T("plan.action.none") : string.Join(", ", parts);
        }

        bool ConfirmShutdown()
        {
            using (var dialog = new CountdownForm(60))
                return dialog.ShowDialog() == DialogResult.OK;
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
                ApplyTrayTexts();
                _shownTooltip = null;
            }

            ApplyAutostart();
            ApplyHotkey();
            RescheduleAutoStop(DateTime.Now);
            SettingsStore.Save(_settings);

            // Hat die Pruefung einen Wert korrigiert (z. B. Hotkey ohne Modifier), Felder nachziehen.
            if (adjusted) _form.LoadSettings(_settings);
            RefreshUi(DateTime.Now);
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

            bool ok = _hotkey.Register(_settings);
            string description = HotkeyInfo.Describe(_settings);
            HotkeyState state = !_settings.HotkeyEnabled || !PlatformInfo.IsWindows
                ? HotkeyState.Off
                : (ok ? HotkeyState.Active : HotkeyState.Failed);
            _form.SystemPage.SetHotkeyStatus(state, description);
        }

        // ------------------------------------------------------------------ Anzeige

        void RefreshUi(DateTime now)
        {
            if (_exiting) return;
            try
            {
                StatusInfo status = StatusBuilder.Build(_engine, _settings, now, _sessionLocked);
                string plan = StatusBuilder.PlanLine(_settings, now, _autoStopDue, status.Kind != StatusKind.WaitingForWindow);

                if (_form.Visible)
                    _form.UpdateStatus(status, plan, _engine.Running, now, _autoStopDue);

                UpdateTray(status);
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }
        }

        void CreateIcons()
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            Icon gray = IconFactory.Create(Theme.Gray, Glyph.Dash, size);
            Icon green = IconFactory.Create(Theme.Green, Glyph.Check, size);
            Icon amber = IconFactory.Create(Theme.Amber, Glyph.Pause, size);
            Icon red = IconFactory.Create(Theme.Red, Glyph.Exclaim, size);
            _icons[StatusKind.Stopped] = gray;
            _icons[StatusKind.Active] = green;
            _icons[StatusKind.StandingBy] = green;
            _icons[StatusKind.WaitingForWindow] = amber;
            _icons[StatusKind.Blocked] = red;
            _icons[StatusKind.SessionLocked] = red;
        }

        NotifyIcon BuildTray()
        {
            var menu = new ContextMenuStrip();
            _miHeader = new ToolStripMenuItem { Enabled = false };
            _miToggle = new ToolStripMenuItem();
            _miShow = new ToolStripMenuItem();
            _miAutostart = new ToolStripMenuItem { CheckOnClick = true };
            _miExit = new ToolStripMenuItem();

            _miToggle.Click += (o, e) => Toggle(Loc.T("reason.tray"));
            _miShow.Click += (o, e) => ShowMainWindow();
            _miAutostart.Click += (o, e) =>
            {
                _settings.StartWithWindows = _miAutostart.Checked;
                OnSettingsChanged();
                _form.LoadSettings(_settings);
                _miAutostart.Checked = _settings.StartWithWindows;
            };
            _miExit.Click += (o, e) => ExitApp();

            menu.Items.Add(_miHeader);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miToggle);
            menu.Items.Add(_miShow);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miAutostart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_miExit);
            menu.Opening += (o, e) => _miAutostart.Checked = _settings.StartWithWindows;

            var tray = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Icon = _icons[StatusKind.Stopped],
                Text = Loc.T("app.title"),
                Visible = true,
            };
            tray.MouseClick += (o, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowMainWindow();
            };
            ApplyTrayTexts(tray);
            return tray;
        }

        void ApplyTrayTexts()
        {
            ApplyTrayTexts(_tray);
        }

        void ApplyTrayTexts(NotifyIcon tray)
        {
            _miShow.Text = Loc.T("tray.show");
            _miAutostart.Text = Loc.T("tray.autostart");
            _miExit.Text = Loc.T("tray.exit");
            _miToggle.Text = Loc.T(_engine != null && _engine.Running ? "btn.stop" : "btn.start");
        }

        void UpdateTray(StatusInfo status)
        {
            if (_tray == null) return;

            if (_shownKind != status.Kind)
            {
                _shownKind = status.Kind;
                _tray.Icon = _icons[status.Kind];
            }

            string tooltip = Truncate(Loc.T("app.title") + " – " + status.Title, 63); // Windows erlaubt max. 63 Zeichen
            if (tooltip != _shownTooltip)
            {
                _shownTooltip = tooltip;
                _tray.Text = tooltip;
            }

            _miHeader.Text = status.Title;
            _miToggle.Text = Loc.T(_engine.Running ? "btn.stop" : "btn.start");
        }

        static string Truncate(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
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
        }

        void HideToTray(bool showHint)
        {
            _form.Hide();
            _form.ShowInTaskbar = false;
            if (showHint && !_balloonShown)
            {
                _balloonShown = true;
                _tray.ShowBalloonTip(4000, Loc.T("app.title"), Loc.T("balloon.tray"), ToolTipIcon.Info);
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
                _hotkey.Dispose();
                _tray.Visible = false;
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

        // ------------------------------------------------------------------ Sitzung (Sperren/Entsperren)

        void HookSystemEvents()
        {
            try { SystemEvents.SessionSwitch += OnSessionSwitch; }
            catch { }
        }

        void UnhookSystemEvents()
        {
            try { SystemEvents.SessionSwitch -= OnSessionSwitch; }
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
                    break;
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                case SessionSwitchReason.SessionLogon:
                    _sessionLocked = false;
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                if (_form != null && !_form.IsDisposed) _form.Dispose();
                foreach (Icon icon in _icons.Values) icon.Dispose();
                if (WindowIcon != null) WindowIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
