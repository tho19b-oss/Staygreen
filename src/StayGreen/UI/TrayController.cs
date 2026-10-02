using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>Was das Tray-Menue vom Programm wissen muss, um passende Eintraege zu zeigen.</summary>
    sealed class TrayState
    {
        public bool Running { get; set; }
        public bool Paused { get; set; }

        /// <summary>Ein wirksamer Zeitplan ist eingeschaltet (nur dann gibt es "Heute aussetzen").</summary>
        public bool ScheduleActive { get; set; }

        public bool SkippedToday { get; set; }
        public bool Autostart { get; set; }
    }

    /// <summary>
    /// Das Symbol im Infobereich mit Zustandsfarbe und Menue. Linksklick zeigt das Fenster, Mittelklick startet oder stoppt.
    /// Das Menue bietet Start/Stopp, Pausieren (15 Minuten bis 2 Stunden), Fortsetzen, "Heute aussetzen" und Beenden.
    /// </summary>
    sealed class TrayController : IDisposable
    {
        /// <summary>Dauern im Untermenue "Pausieren" in Minuten.</summary>
        static readonly int[] PauseMinutes = { 15, 30, 60, 120 };

        readonly NotifyIcon _icon;
        readonly ContextMenuStrip _menu = new ContextMenuStrip();
        readonly Dictionary<StatusKind, Icon> _icons = new Dictionary<StatusKind, Icon>();

        readonly ToolStripMenuItem _header = new ToolStripMenuItem { Enabled = false };
        readonly ToolStripMenuItem _toggle = new ToolStripMenuItem();
        readonly ToolStripMenuItem _pause = new ToolStripMenuItem();
        readonly ToolStripMenuItem _resume = new ToolStripMenuItem();
        readonly ToolStripMenuItem _skipToday = new ToolStripMenuItem();
        readonly ToolStripMenuItem _show = new ToolStripMenuItem();
        readonly ToolStripMenuItem _autostart = new ToolStripMenuItem { CheckOnClick = true };
        readonly ToolStripMenuItem _exit = new ToolStripMenuItem();

        StatusKind? _shownKind;
        string _shownTooltip;
        TrayState _state = new TrayState();

        public TrayController()
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            _icons[StatusKind.Stopped] = IconFactory.Create(Theme.Gray, Glyph.Dash, size);
            Icon green = IconFactory.Create(Theme.Green, Glyph.Check, size);
            Icon amber = IconFactory.Create(Theme.Amber, Glyph.Pause, size);
            Icon red = IconFactory.Create(Theme.Red, Glyph.Exclaim, size);
            _icons[StatusKind.Active] = green;
            _icons[StatusKind.StandingBy] = green;
            _icons[StatusKind.WaitingForWindow] = amber;
            _icons[StatusKind.Paused] = amber;
            _icons[StatusKind.WaitingForTeams] = amber;
            _icons[StatusKind.Blocked] = red;
            _icons[StatusKind.SessionLocked] = red;

            _toggle.Click += (o, e) => Raise(ToggleRequested);
            _resume.Click += (o, e) => Raise(ResumeRequested);
            _skipToday.Click += (o, e) => Raise(SkipTodayRequested);
            _show.Click += (o, e) => Raise(ShowRequested);
            _exit.Click += (o, e) => Raise(ExitRequested);
            _autostart.Click += (o, e) =>
            {
                Action<bool> handler = AutostartRequested;
                if (handler != null) handler(_autostart.Checked);
            };

            foreach (int minutes in PauseMinutes)
            {
                int m = minutes;
                var item = new ToolStripMenuItem { Tag = m };
                item.Click += (o, e) =>
                {
                    Action<int> handler = PauseRequested;
                    if (handler != null) handler(m);
                };
                _pause.DropDownItems.Add(item);
            }

            _menu.Items.Add(_header);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_toggle);
            _menu.Items.Add(_pause);
            _menu.Items.Add(_resume);
            _menu.Items.Add(_skipToday);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_show);
            _menu.Items.Add(_autostart);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_exit);
            _menu.Opening += (o, e) => ApplyState();

            _icon = new NotifyIcon
            {
                ContextMenuStrip = _menu,
                Icon = _icons[StatusKind.Stopped],
                Text = Loc.T("app.title"),
                Visible = true,
            };
            _icon.MouseClick += (o, e) =>
            {
                if (e.Button == MouseButtons.Left) Raise(ShowRequested);
                else if (e.Button == MouseButtons.Middle) Raise(ToggleRequested);
            };

            ApplyTexts();
            ApplyState();
        }

        public event Action ToggleRequested;
        public event Action ShowRequested;
        public event Action ResumeRequested;
        public event Action SkipTodayRequested;
        public event Action ExitRequested;
        public event Action<int> PauseRequested;
        public event Action<bool> AutostartRequested;

        static void Raise(Action handler)
        {
            if (handler != null) handler();
        }

        /// <summary>Setzt alle Beschriftungen in der aktuellen Sprache.</summary>
        public void ApplyTexts()
        {
            _show.Text = Loc.T("tray.show");
            _autostart.Text = Loc.T("tray.autostart");
            _exit.Text = Loc.T("tray.exit");
            _pause.Text = Loc.T("tray.pause");
            _resume.Text = Loc.T("tray.resume");
            foreach (ToolStripMenuItem item in _pause.DropDownItems)
                item.Text = StatusBuilder.PauseLabel((int)item.Tag);
            _shownTooltip = null;
            ApplyState();
        }

        /// <summary>Icon, Tooltip und Menue dem aktuellen Zustand anpassen (einmal pro Sekunde).</summary>
        public void Update(StatusInfo status, TrayState state)
        {
            _state = state;

            if (_shownKind != status.Kind)
            {
                _shownKind = status.Kind;
                _icon.Icon = _icons[status.Kind];
            }

            string tooltip = Truncate(Loc.T("app.title") + " – " + status.Title, 63); // Windows erlaubt max. 63 Zeichen
            if (tooltip != _shownTooltip)
            {
                _shownTooltip = tooltip;
                _icon.Text = tooltip;
            }

            _header.Text = status.Title;
            ApplyState();
        }

        void ApplyState()
        {
            _toggle.Text = Loc.T(_state.Running ? "btn.stop" : "btn.start");
            _pause.Enabled = _state.Running && !_state.Paused;
            _resume.Visible = _state.Paused;
            _skipToday.Visible = _state.ScheduleActive;
            _skipToday.Text = Loc.T(_state.SkippedToday ? "tray.unskiptoday" : "tray.skiptoday");
            _autostart.Checked = _state.Autostart;
        }

        public void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            try { _icon.ShowBalloonTip(4000, title, text, icon); }
            catch { }
        }

        static string Truncate(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
            foreach (Icon icon in new HashSet<Icon>(_icons.Values)) icon.Dispose();
        }
    }
}
