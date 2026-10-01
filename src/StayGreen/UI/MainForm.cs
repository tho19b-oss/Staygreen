using System;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;
using StayGreen.Platform;

namespace StayGreen.UI
{
    /// <summary>Pixelwerte in Abhaengigkeit von der Windows-Skalierung (100 %, 125 %, 150 % ...).</summary>
    static class Dpi
    {
        static float _factor;

        public static float Factor
        {
            get
            {
                if (_factor <= 0f)
                {
                    try
                    {
                        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                            _factor = Math.Max(1f, g.DpiX / 96f);
                    }
                    catch
                    {
                        _factor = 1f;
                    }
                }
                return _factor;
            }
        }

        public static int Px(int value)
        {
            return (int)Math.Round(value * Factor);
        }
    }

    /// <summary>Hauptfenster: Statuskarte mit Start/Stopp oben, darunter die Einstellungen in vier Registerkarten.</summary>
    sealed class MainForm : Form
    {
        readonly AppController _app;
        readonly Panel _header = new Panel();
        readonly StatusLamp _lamp = new StatusLamp { Size = new Size(52, 52) };
        readonly Label _title = new Label();
        readonly WrapLabel _detail = new WrapLabel { AutoSize = false };
        readonly WrapLabel _plan = new WrapLabel { AutoSize = false, ForeColor = Theme.Muted };
        readonly Button _toggle = new Button();
        readonly TabControl _tabs = new TabControl();
        readonly TabPage _tabActivity = new TabPage();
        readonly TabPage _tabSchedule = new TabPage();
        readonly TabPage _tabStop = new TabPage();
        readonly TabPage _tabSystem = new TabPage();
        readonly Label _footer = new Label();

        public ActivityPage ActivityPage { get; private set; }
        public SchedulePage SchedulePage { get; private set; }
        public AutoStopPage AutoStopPage { get; private set; }
        public SystemPage SystemPage { get; private set; }

        public MainForm(AppController app)
        {
            _app = app;

            // Skalierung ueber die Bildschirm-DPI (nicht ueber Schriftmasse): exakt 100/125/150 %, auch fuer
            // Abstaende und Groessen, die im Code festgelegt sind.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            ClientSize = new Size(500, 640);
            MinimumSize = new Size(480, 520); // logische Pixel: AutoScaleMode.Dpi skaliert das selbst
            Icon = _app.WindowIcon;

            BuildHeader();
            BuildTabs();

            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 26;
            _footer.TextAlign = ContentAlignment.MiddleCenter;
            _footer.ForeColor = Theme.Muted;

            // Reihenfolge: zuerst die Fuellflaeche, dann die Raender (so macht es auch der Designer).
            Controls.Add(_tabs);
            Controls.Add(_footer);
            Controls.Add(_header);

            ApplyTexts();
        }

        void BuildHeader()
        {
            _header.Dock = DockStyle.Top;
            _header.Height = 120;

            _title.AutoSize = false;
            _title.UseMnemonic = false;
            _title.Font = new Font(Font.FontFamily, Font.Size + 3f, FontStyle.Bold);

            _toggle.AutoSize = false;
            _toggle.Size = new Size(116, 42);
            _toggle.FlatStyle = FlatStyle.Flat;
            _toggle.FlatAppearance.BorderSize = 0;
            _toggle.ForeColor = Color.White;
            _toggle.Font = new Font(Font.FontFamily, Font.Size + 1f, FontStyle.Bold);
            _toggle.Cursor = Cursors.Hand;
            _toggle.UseVisualStyleBackColor = false;
            _toggle.Click += (o, e) => _app.Toggle(Loc.T("reason.manual"));

            _header.Controls.Add(_lamp);
            _header.Controls.Add(_title);
            _header.Controls.Add(_detail);
            _header.Controls.Add(_plan);
            _header.Controls.Add(_toggle);
            _header.Resize += (o, e) => LayoutHeader();
        }

        /// <summary>
        /// Ordnet die Statuskarte von Hand an (Lampe | Titel ... Knopf, darunter Detail- und Planzeile) und passt
        /// ihre Hoehe an den umgebrochenen Text an. Deterministisch, ohne AutoSize-Ketten.
        /// </summary>
        void LayoutHeader()
        {
            int width = _header.ClientSize.Width;
            if (width <= 0) return;

            int pad = Dpi.Px(14);
            int top = Dpi.Px(12);
            int gap = Dpi.Px(12);

            _lamp.Location = new Point(pad, top);
            _toggle.Location = new Point(width - pad - _toggle.Width, top);

            int textLeft = pad + _lamp.Width + gap;
            int titleWidth = Math.Max(Dpi.Px(120), _toggle.Left - gap - textLeft);
            int fullWidth = Math.Max(Dpi.Px(120), width - pad - textLeft);

            int titleHeight = Measure(_title, titleWidth);
            _title.SetBounds(textLeft, top + Dpi.Px(2), titleWidth, titleHeight);

            // Darunter beginnt alles unterhalb von Titel und Knopf (so ueberlappt nie etwas den Knopf).
            int y = Math.Max(_title.Bottom, _toggle.Bottom) + Dpi.Px(4);

            int detailHeight = Measure(_detail, fullWidth);
            _detail.SetBounds(textLeft, y, fullWidth, detailHeight);
            y = _detail.Bottom + Dpi.Px(2);

            bool hasPlan = !string.IsNullOrEmpty(_plan.Text);
            if (hasPlan)
            {
                int planHeight = Measure(_plan, fullWidth);
                _plan.SetBounds(textLeft, y, fullWidth, planHeight);
                y = _plan.Bottom;
            }
            _plan.Visible = hasPlan;

            int height = Math.Max(_lamp.Bottom, y) + Dpi.Px(10);
            if (_header.Height != height) _header.Height = height;
        }

        static int Measure(Label label, int width)
        {
            string text = string.IsNullOrEmpty(label.Text) ? " " : label.Text;
            return TextRenderer.MeasureText(text, label.Font, new Size(width, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height + Dpi.Px(2);
        }

        void BuildTabs()
        {
            _tabs.Dock = DockStyle.Fill;
            _tabs.Padding = new Point(14, 4);

            ActivityPage = new ActivityPage();
            SchedulePage = new SchedulePage();
            AutoStopPage = new AutoStopPage();
            SystemPage = new SystemPage { IsPortable = SettingsStore.IsPortable };

            _tabActivity.Controls.Add(ActivityPage);
            _tabSchedule.Controls.Add(SchedulePage);
            _tabStop.Controls.Add(AutoStopPage);
            _tabSystem.Controls.Add(SystemPage);
            _tabs.TabPages.AddRange(new[] { _tabActivity, _tabSchedule, _tabStop, _tabSystem });

            ActivityPage.Changed += _app.OnSettingsChanged;
            SchedulePage.Changed += _app.OnSettingsChanged;
            AutoStopPage.Changed += _app.OnSettingsChanged;
            SystemPage.Changed += _app.OnSettingsChanged;
        }

        /// <summary>Befuellt alle Karten aus den Einstellungen (nach dem Start und nach programmatischen Aenderungen).</summary>
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
            _tabActivity.Text = Loc.T("tab.activity");
            _tabSchedule.Text = Loc.T("tab.schedule");
            _tabStop.Text = Loc.T("tab.stop");
            _tabSystem.Text = Loc.T("tab.system");
            _footer.Text = Loc.T("footer", AppInfo.Version);
            ActivityPage.ApplyTexts();
            SchedulePage.ApplyTexts();
            AutoStopPage.ApplyTexts();
            SystemPage.ApplyTexts();
        }

        /// <summary>Wird jede Sekunde vom Controller aufgerufen.</summary>
        public void UpdateStatus(StatusInfo status, string planLine, bool running, DateTime now, DateTime? autoStopDue)
        {
            _lamp.Set(Theme.ColorFor(status.Kind), Theme.GlyphFor(status.Kind));

            bool changed = SetText(_title, status.Title);
            changed |= SetText(_detail, status.Detail);
            changed |= SetText(_plan, planLine);
            changed |= SetText(_toggle, Loc.T(running ? "btn.stop" : "btn.start"));
            if (changed) LayoutHeader();

            Color back = running ? Theme.GrayDark : Theme.Green;
            if (_toggle.BackColor != back)
            {
                _toggle.BackColor = back;
                _toggle.FlatAppearance.MouseOverBackColor = running ? Color.FromArgb(0x35, 0x39, 0x40) : Theme.GreenDark;
                _toggle.FlatAppearance.MouseDownBackColor = running ? Color.FromArgb(0x2A, 0x2D, 0x33) : Color.FromArgb(0x1F, 0x70, 0x3D);
            }

            // Nur die sichtbare Karte aktualisieren (die Zeilen dort aendern sich mit der Uhrzeit).
            PageBase page = CurrentPage();
            if (page != null) page.Tick(now, autoStopDue);
        }

        PageBase CurrentPage()
        {
            if (_tabs.SelectedTab == _tabSchedule) return SchedulePage;
            if (_tabs.SelectedTab == _tabStop) return AutoStopPage;
            if (_tabs.SelectedTab == _tabActivity) return ActivityPage;
            return SystemPage;
        }

        static bool SetText(Control control, string text)
        {
            text = text ?? "";
            if (control.Text == text) return false;
            control.Text = text;
            return true;
        }

        public void SelectTab(int index)
        {
            if (index >= 0 && index < _tabs.TabPages.Count) _tabs.SelectedIndex = index;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized) _app.OnMainWindowMinimized();
        }

        // Die DPI-Skalierung laeuft waehrend des ersten Anzeigens; danach die Statuskarte sicher neu anordnen,
        // auch wenn sich ihre Texte (z. B. im Zustand "Gestoppt") nicht mehr aendern.
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LayoutHeader();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LayoutHeader();
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
