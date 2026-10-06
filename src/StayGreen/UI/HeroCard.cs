using System;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Die grosse Statuskarte oben im Fenster: Lampe, Zustand, Details, Zeitplan-Zeile und der Start/Stopp-Knopf.
    /// Solange StayGreen laeuft, folgt eine Zeile mit den Pausen-Knoepfen, waehrend einer Pause stattdessen "Fortsetzen"
    /// neben dem Stopp-Knopf. Der Hintergrund faerbt sich zart passend zum Zustand (gruen, gelb, rot, grau).
    /// </summary>
    sealed class HeroCard : LayoutPanel
    {
        /// <summary>Dauern der Pausen-Knoepfe in Minuten.</summary>
        static readonly int[] PauseMinutes = { 30, 60, 120 };

        readonly StatusLamp _lamp = new StatusLamp();
        readonly WrapLabel _title = new WrapLabel();
        readonly WrapLabel _detail = new WrapLabel();
        readonly WrapLabel _plan = new WrapLabel();
        readonly FlatButton _toggle = new FlatButton { MinWidth = 100 };
        readonly FlatButton _resume = new FlatButton { MinWidth = 100, Kind = ButtonKind.Secondary, Visible = false };
        readonly Label _pauseLabel = new Label { AutoSize = true, UseMnemonic = false, Visible = false };
        readonly FlatButton[] _pauseButtons = new FlatButton[PauseMinutes.Length];

        Color _tint = Theme.TintFor(StatusKind.Stopped);
        Color _tintBorder = Theme.TintBorderFor(StatusKind.Stopped);

        // Ob die Pausen-Zeile gezeigt wird. Eigener Merker, weil Control.Visible auch vom Elternfenster abhaengt und
        // die Anordnung schon vor dem ersten Anzeigen messen muss.
        bool _canPause;

        public HeroCard()
        {
            _title.ForeColor = Theme.Text;
            _detail.ForeColor = Theme.Text;
            _plan.ForeColor = Theme.MutedStrong;
            _pauseLabel.ForeColor = Theme.MutedStrong;
            _lamp.Size = new Size(Dpi.Px(48), Dpi.Px(48));
            _toggle.Click += (o, e) =>
            {
                Action handler = ToggleClicked;
                if (handler != null) handler();
            };
            _resume.Click += (o, e) =>
            {
                Action handler = ResumeClicked;
                if (handler != null) handler();
            };

            Controls.Add(_lamp);
            Controls.Add(_title);
            Controls.Add(_detail);
            Controls.Add(_plan);
            Controls.Add(_toggle);
            Controls.Add(_resume);
            Controls.Add(_pauseLabel);
            for (int i = 0; i < PauseMinutes.Length; i++)
            {
                int minutes = PauseMinutes[i];
                var button = new FlatButton { Kind = ButtonKind.Secondary, Visible = false };
                button.Click += (o, e) =>
                {
                    Action<int> handler = PauseClicked;
                    if (handler != null) handler(minutes);
                };
                _pauseButtons[i] = button;
                Controls.Add(button);
            }
            ApplyTint();
            UpdateFonts();
        }

        /// <summary>Der Start/Stopp-Knopf wurde geklickt.</summary>
        public event Action ToggleClicked;

        /// <summary>Der "Fortsetzen"-Knopf (nur waehrend einer Pause sichtbar) wurde geklickt.</summary>
        public event Action ResumeClicked;

        /// <summary>Ein Pausen-Knopf (nur sichtbar, solange gestartet und nicht pausiert) wurde geklickt; Wert: Dauer in Minuten.</summary>
        public event Action<int> PauseClicked;

        /// <summary>Setzt die Anzeige; True, wenn sich Texte geaendert haben (dann muss neu angeordnet werden).</summary>
        public bool SetState(StatusKind kind, string title, string detail, string plan, bool running)
        {
            _lamp.Set(Theme.ColorFor(kind), Theme.GlyphFor(kind));

            Color tint = Theme.TintFor(kind);
            if (tint != _tint)
            {
                _tint = tint;
                _tintBorder = Theme.TintBorderFor(kind);
                ApplyTint();
                Invalidate();
            }

            bool changed = SetText(_title, title);
            changed |= SetText(_detail, detail);
            changed |= SetText(_plan, plan);
            changed |= SetText(_toggle, Loc.T(running ? "btn.stop" : "btn.start"));
            changed |= SetText(_resume, Loc.T("btn.resume"));
            _toggle.Kind = running ? ButtonKind.Dark : ButtonKind.Primary;
            _plan.Visible = !string.IsNullOrEmpty(plan);

            bool paused = kind == StatusKind.Paused;
            if (_resume.Visible != paused)
            {
                _resume.Visible = paused;
                changed = true;
            }

            // Pausieren geht nur bei laufendem Aktivhalten und nicht doppelt (wie im Tray-Menue); sonst bleibt die Zeile weg.
            bool canPause = running && !paused;
            if (_canPause != canPause)
            {
                _canPause = canPause;
                _pauseLabel.Visible = canPause;
                foreach (FlatButton button in _pauseButtons) button.Visible = canPause;
                changed = true;
            }

            string pauseText = Loc.T("hero.pause");
            changed |= SetText(_pauseLabel, pauseText);
            for (int i = 0; i < PauseMinutes.Length; i++)
            {
                string text = StatusBuilder.PauseLabel(PauseMinutes[i]);
                changed |= SetText(_pauseButtons[i], text);

                // Fuer Screenreader mit Zusammenhang: "Pause fuer 30 Minuten" statt nur "30 Minuten".
                string name = pauseText.TrimEnd(':') + " " + text;
                if (_pauseButtons[i].AccessibleName != name) _pauseButtons[i].AccessibleName = name;
            }
            return changed;
        }

        static bool SetText(Control control, string text)
        {
            text = text ?? "";
            if (control.Text == text) return false;
            control.Text = text;
            return true;
        }

        void ApplyTint()
        {
            // Die Knoepfe malen ihre Ecken in der Farbe des Elternteils; damit sie auf der Tonung (und im dunklen Design)
            // keinen hellen Rand bekommen, traegt auch die Karte selbst die Tonung als BackColor.
            BackColor = _tint;
            _lamp.BackColor = _tint;
            _title.BackColor = _tint;
            _detail.BackColor = _tint;
            _plan.BackColor = _tint;
            _pauseLabel.BackColor = _tint;
        }

        void UpdateFonts()
        {
            Font oldTitle = _title.Font;
            _title.Font = new Font(Font.FontFamily, Font.Size + 2.5f, FontStyle.Bold);
            if (!ReferenceEquals(oldTitle, Font)) oldTitle.Dispose();

            Font oldToggle = _toggle.Font;
            _toggle.Font = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold);
            if (!ReferenceEquals(oldToggle, Font)) oldToggle.Dispose();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateFonts();
        }

        // ------------------------------------------------------------------ Anordnung

        public override int MeasureHeight(int width)
        {
            return Arrange(width, false);
        }

        protected override void DoLayout()
        {
            Arrange(ClientSize.Width, true);
        }

        /// <summary>
        /// Lampe links, Titel und Knoepfe in einer Zeile, darunter Details, Zeitplan und (beim Laufen) die Pausen-Zeile.
        /// Liefert die Gesamthoehe.
        /// </summary>
        int Arrange(int width, bool apply)
        {
            int pad = Dpi.Px(16);
            int gap = Dpi.Px(14);
            int lamp = _lamp.Width;
            int textLeft = pad + lamp + gap;
            Size button = _toggle.Size;
            Size resume = _resume.Visible ? _resume.Size : Size.Empty;
            int buttons = button.Width + (resume.Width > 0 ? resume.Width + Dpi.Px(8) : 0);

            int titleWidth = Math.Max(Dpi.Px(100), width - pad - buttons - gap - textLeft);
            int fullWidth = Math.Max(Dpi.Px(100), width - pad - textLeft);

            int titleHeight = _title.MeasureHeight(titleWidth);
            int rowHeight = Math.Max(titleHeight, button.Height);
            int y = pad;

            if (apply)
            {
                _lamp.Location = new Point(pad, pad);
                _toggle.Location = new Point(width - pad - button.Width, pad + (rowHeight - button.Height) / 2);
                if (resume.Width > 0)
                    _resume.Location = new Point(_toggle.Left - Dpi.Px(8) - resume.Width, pad + (rowHeight - resume.Height) / 2);
                _title.SetBounds(textLeft, pad + (rowHeight - titleHeight) / 2, titleWidth, titleHeight);
            }
            y += rowHeight + Dpi.Px(2);

            int detailHeight = _detail.MeasureHeight(fullWidth);
            if (apply) _detail.SetBounds(textLeft, y, fullWidth, detailHeight);
            y += detailHeight;

            if (!string.IsNullOrEmpty(_plan.Text))
            {
                y += Dpi.Px(2);
                int planHeight = _plan.MeasureHeight(fullWidth);
                if (apply) _plan.SetBounds(textLeft, y, fullWidth, planHeight);
                y += planHeight;
            }

            // Die Pausen-Zeile beginnt am linken Kartenrand (unter der Lampe), damit sie die ganze Breite nutzen kann.
            if (_canPause) y = ArrangePauseRow(pad, width - pad, y + Dpi.Px(10), apply);

            return Math.Max(pad + lamp, y) + pad;
        }

        /// <summary>
        /// Beschriftung und Pausen-Knoepfe nebeneinander ab <paramref name="left"/>. Wird die Zeile zu schmal (grosse
        /// Schrift, andere Sprache), laufen die Knoepfe in die naechste Zeile. Liefert die untere Kante der Zeile.
        /// </summary>
        int ArrangePauseRow(int left, int right, int top, bool apply)
        {
            Size label = _pauseLabel.GetPreferredSize(Size.Empty);
            int lineHeight = label.Height;
            foreach (FlatButton button in _pauseButtons) lineHeight = Math.Max(lineHeight, button.Height);

            int gap = Dpi.Px(8);
            int x = left;
            int y = top;

            void Place(Control control, Size size)
            {
                if (x > left && x + size.Width > right)
                {
                    x = left;
                    y += lineHeight + gap;
                }
                if (apply) control.Location = new Point(x, y + (lineHeight - size.Height) / 2);
                x += size.Width + gap;
            }

            Place(_pauseLabel, label);
            foreach (FlatButton button in _pauseButtons) Place(button, button.Size);
            return y + lineHeight;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Background);
            Metrics.PaintRounded(e.Graphics, ClientRectangle, Dpi.Px(14), _tint, _tintBorder);
        }
    }
}
