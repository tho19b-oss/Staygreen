using System;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;
using StayGreen.Platform;

namespace StayGreen.UI
{
    /// <summary>
    /// Hauptfenster: oben die Statuskarte mit Start/Stopp, darunter eine Leiste mit drei Seiten
    /// (Aktivitaet, Zeitplan, System) und ganz unten die Versionszeile.
    /// </summary>
    sealed class MainForm : Form
    {
        const int PageCount = 3;

        readonly AppController _app;
        readonly HeroCard _hero = new HeroCard();
        readonly Segmented _nav = new Segmented();
        readonly ScrollHost[] _hosts = new ScrollHost[PageCount];
        readonly Label _footer = new Label();
        bool _built;

        public ActivityPage ActivityPage { get; private set; }
        public SchedulePage SchedulePage { get; private set; }
        public AutoStopPage AutoStopPage { get; private set; }
        public SystemPage SystemPage { get; private set; }

        public MainForm(AppController app)
        {
            _app = app;

            // Alle Abstaende werden ueber Dpi.Px selbst skaliert; die Schrift kommt skaliert von Windows.
            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;
            BackColor = Theme.Background;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimumSize = new Size(Dpi.Px(460), Dpi.Px(540));
            ClientSize = new Size(Dpi.Px(480), StartHeight());
            Icon = _app.WindowIcon;

            _hero.ToggleClicked += () => _app.Toggle(Loc.T("reason.manual"));
            _hero.ResumeClicked += () => _app.ResumeFromPause();
            _hero.PauseClicked += minutes => _app.PauseFor(minutes, Loc.T("reason.manual"));
            _nav.SelectedIndexChanged += (o, e) => ShowSelectedPage();

            _footer.AutoSize = false;
            _footer.TextAlign = ContentAlignment.MiddleCenter;
            _footer.ForeColor = Theme.Muted;
            _footer.UseMnemonic = false;

            BuildPages();

            Controls.Add(_hero);
            Controls.Add(_nav);
            foreach (ScrollHost host in _hosts) Controls.Add(host);
            Controls.Add(_footer);

            _built = true;
            ApplyTexts();
            ShowSelectedPage();
        }

        /// <summary>Hoehe beim ersten Oeffnen: ausreichend, aber nie hoeher als der Bildschirm.</summary>
        static int StartHeight()
        {
            int desired = Dpi.Px(700);
            int available = Screen.PrimaryScreen.WorkingArea.Height - Dpi.Px(90);
            return Math.Max(Dpi.Px(480), Math.Min(desired, available));
        }

        void BuildPages()
        {
            ActivityPage = new ActivityPage();
            SchedulePage = new SchedulePage();
            AutoStopPage = new AutoStopPage();
            SystemPage = new SystemPage { IsPortable = SettingsStore.IsPortable };

            _hosts[0] = CreateHost(ActivityPage);
            _hosts[1] = CreateHost(SchedulePage, AutoStopPage);
            _hosts[2] = CreateHost(SystemPage);

            ActivityPage.Changed += _app.OnSettingsChanged;
            SchedulePage.Changed += _app.OnSettingsChanged;
            AutoStopPage.Changed += _app.OnSettingsChanged;
            SystemPage.Changed += _app.OnSettingsChanged;
        }

        static ScrollHost CreateHost(params PageBase[] pages)
        {
            var content = new Stack { Inset = new Padding(16, 4, 16, 20), Gap = 12 };
            foreach (PageBase page in pages) content.Controls.Add(page);
            return new ScrollHost(content);
        }

        /// <summary>Befuellt alle Seiten aus den Einstellungen (nach dem Start und nach programmatischen Aenderungen).</summary>
        public void LoadSettings(Settings settings)
        {
            ActivityPage.LoadSettings(settings);
            SchedulePage.LoadSettings(settings);
            AutoStopPage.LoadSettings(settings);
            SystemPage.LoadSettings(settings);
        }

        public void ApplyTexts()
        {
            Text = Loc.T("app.title");
            _nav.Items = new[] { Loc.T("tab.activity"), Loc.T("tab.schedule"), Loc.T("tab.system") };
            _footer.Text = Loc.T("footer", AppInfo.Version);
            ActivityPage.ApplyTexts();
            SchedulePage.ApplyTexts();
            AutoStopPage.ApplyTexts();
            SystemPage.ApplyTexts();
            LayoutAll();
        }

        /// <summary>Wird jede Sekunde vom Controller aufgerufen.</summary>
        public void UpdateStatus(StatusInfo status, string planLine, bool running, DateTime now, DateTime? autoStopDue)
        {
            if (_hero.SetState(status.Kind, status.Title, status.Detail, planLine, running)) LayoutAll();

            // Nur die sichtbare Seite aktualisieren (die Zeilen dort aendern sich mit der Uhrzeit).
            switch (_nav.SelectedIndex)
            {
                case 0:
                    ActivityPage.Tick(now, autoStopDue);
                    break;
                case 1:
                    SchedulePage.Tick(now, autoStopDue);
                    AutoStopPage.Tick(now, autoStopDue);
                    break;
                default:
                    SystemPage.Tick(now, autoStopDue);
                    break;
            }
        }

        /// <summary>Die gerade gezeigte Seite (0 Aktivitaet, 1 Zeitplan, 2 System).</summary>
        public int SelectedTab
        {
            get { return _nav.SelectedIndex; }
        }

        /// <summary>Zeigt die Seite mit diesem Index (0 Aktivitaet, 1 Zeitplan, 2 System).</summary>
        public void SelectTab(int index)
        {
            if (index >= 0 && index < PageCount) _nav.SelectedIndex = index;
        }

        void ShowSelectedPage()
        {
            for (int i = 0; i < PageCount; i++) _hosts[i].Visible = i == _nav.SelectedIndex;
            LayoutAll();
        }

        // ------------------------------------------------------------------ Anordnung

        void LayoutAll()
        {
            // Windows meldet schon beim Einrichten des Fensters ein Layout, bevor alle Teile existieren.
            if (!_built) return;

            int width = ClientSize.Width;
            int height = ClientSize.Height;
            if (width <= 0 || height <= 0) return;

            int margin = Dpi.Px(16);
            int inner = Math.Max(1, width - 2 * margin);

            int heroHeight = _hero.MeasureHeight(inner);
            Stack.PlaceChild(_hero, new Rectangle(margin, margin, inner, heroHeight));
            int y = margin + heroHeight + Dpi.Px(14);

            int navHeight = _nav.PreferredHeight;
            _nav.SetBounds(margin, y, inner, navHeight);
            y += navHeight + Dpi.Px(12);

            int footerHeight = Dpi.Px(28);
            _footer.SetBounds(0, height - footerHeight, width, footerHeight);

            int pageHeight = Math.Max(0, height - footerHeight - y);
            foreach (ScrollHost host in _hosts) host.SetBounds(0, y, width, pageHeight);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            LayoutAll();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) _app.OnMainWindowMinimized();
        }

        // Das Mausrad scrollt die sichtbare Seite auch dann, wenn gerade die Leiste oder ein Knopf den Fokus hat.
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ScrollHost host = _hosts[Math.Max(0, Math.Min(PageCount - 1, _nav.SelectedIndex))];
            if (!host.ContainsFocus) host.ScrollByWheel(e.Delta);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyTitleBar(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutAll();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_app.IsExiting)
            {
                if (!_app.OnMainWindowClosing())
                {
                    e.Cancel = true;
                    return;
                }
            }
            else if (e.CloseReason == CloseReason.WindowsShutDown || e.CloseReason == CloseReason.TaskManagerClosing)
            {
                // Abmelden/Herunterfahren: Ende im Protokoll festhalten und Wach-Halten freigeben.
                _app.ExitApp();
            }
            base.OnFormClosing(e);
        }
    }

    static class AppInfo
    {
        /// <summary>Hierhin fuehrt "Neue Version suchen": Die Seite oeffnet im Browser, StayGreen selbst verbindet sich nie.</summary>
        public const string ReleaseUrl = "https://github.com/tho19b-oss/Staygreen/releases/latest";

        public static string Version
        {
            get
            {
                Version v = typeof(AppInfo).Assembly.GetName().Version;
                return v == null ? "1.0" : v.Major + "." + v.Minor + "." + v.Build;
            }
        }
    }
}
