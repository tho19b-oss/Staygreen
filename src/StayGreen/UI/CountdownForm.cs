using System;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Vorwarnung vor dem Auto-Stopp (Teams beenden, Sperren, Herunterfahren): Countdown bis zum Termin mit den Wahlmoeglichkeiten
    /// Abbrechen, Verschieben und Jetzt ausfuehren. Der Anfangsfokus liegt auf "Abbrechen", damit ein versehentlicher Druck
    /// auf Enter oder Leertaste (z. B. beim Tippen) nichts ausfuehrt. Laeuft der Countdown ab, schliesst der Dialog von selbst
    /// mit dem Ergebnis <see cref="AutoStopChoice.Timeout"/> (= ausfuehren).
    /// </summary>
    sealed class CountdownForm : Form
    {
        readonly DateTime _due;
        readonly Func<DateTime> _clock;
        readonly string _actions;
        readonly WrapLabel _text = new WrapLabel { ForeColor = Theme.Text };
        readonly FlatButton _cancel = new FlatButton { Kind = ButtonKind.Primary, MinWidth = 120 };
        readonly FlatButton _snooze = new FlatButton { Kind = ButtonKind.Secondary, MinWidth = 120 };
        readonly FlatButton _now = new FlatButton { Kind = ButtonKind.Secondary, MinWidth = 120 };
        readonly Timer _timer = new Timer { Interval = 250 };
        bool _decided;
        int _shownSeconds = int.MinValue;

        /// <param name="due">Zeitpunkt, zu dem ausgefuehrt wird.</param>
        /// <param name="clock">Uhr (fuer Tests austauschbar).</param>
        /// <param name="actions">Lesbare Liste der Aktionen.</param>
        /// <param name="snoozeMinutes">Um so viele Minuten verschiebt der mittlere Knopf.</param>
        public CountdownForm(DateTime due, Func<DateTime> clock, string actions, int snoozeMinutes)
        {
            _due = due;
            _clock = clock ?? (() => DateTime.Now);
            _actions = actions ?? "";
            Choice = AutoStopChoice.Timeout;

            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;
            BackColor = Theme.Background;
            Text = Loc.T("cd.title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;

            _cancel.Text = Loc.T("cd.cancel");
            _snooze.Text = Loc.T("cd.snooze", snoozeMinutes);
            _now.Text = Loc.T("cd.now");
            _cancel.Click += (o, e) => Decide(AutoStopChoice.Cancel);
            _snooze.Click += (o, e) => Decide(AutoStopChoice.Snooze);
            _now.Click += (o, e) => Decide(AutoStopChoice.RunNow);

            Controls.Add(_text);
            Controls.Add(_cancel);
            Controls.Add(_snooze);
            Controls.Add(_now);
            CancelButton = _cancel;
            AcceptButton = _cancel; // Enter bricht ab: lieber zu vorsichtig als versehentlich etwas auszufuehren
            ActiveControl = _cancel;

            UpdateText();
            ArrangeControls();

            _timer.Tick += (o, e) =>
            {
                if (_due - _clock() <= TimeSpan.Zero)
                {
                    Decide(AutoStopChoice.Timeout);
                    return;
                }
                UpdateText();
            };
        }

        /// <summary>Was der Nutzer gewaehlt hat (Timeout, wenn der Countdown abgelaufen ist).</summary>
        public AutoStopChoice Choice { get; private set; }

        void Decide(AutoStopChoice choice)
        {
            if (_decided) return;
            _decided = true;
            Choice = choice;
            _timer.Stop();
            Close();
        }

        void ArrangeControls()
        {
            int margin = Dpi.Px(20);
            int width = Dpi.Px(560);
            int inner = width - 2 * margin;

            int textHeight = Math.Max(_text.MeasureHeight(inner), Dpi.Px(48));
            _text.SetBounds(margin, margin, inner, textHeight);

            int gap = Dpi.Px(10);
            int buttonWidth = (inner - 2 * gap) / 3;
            int buttonHeight = _cancel.Height;
            int y = margin + textHeight + Dpi.Px(16);
            _cancel.SetBounds(margin, y, buttonWidth, buttonHeight);
            _snooze.SetBounds(margin + buttonWidth + gap, y, buttonWidth, buttonHeight);
            _now.SetBounds(margin + 2 * (buttonWidth + gap), y, inner - 2 * (buttonWidth + gap), buttonHeight);

            ClientSize = new Size(width, y + buttonHeight + margin);
        }

        void UpdateText()
        {
            int seconds = (int)Math.Ceiling(Math.Max(0, (_due - _clock()).TotalSeconds));
            if (seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            _text.Text = Loc.T("cd.text", seconds, _actions);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.ApplyTitleBar(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
            Activate();
            _cancel.Focus();
            try { System.Media.SystemSounds.Asterisk.Play(); }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Mit dem X geschlossen (oder Alt+F4): Das gilt als Abbrechen, nicht als "ausfuehren".
            if (!_decided)
            {
                _decided = true;
                Choice = AutoStopChoice.Cancel;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
