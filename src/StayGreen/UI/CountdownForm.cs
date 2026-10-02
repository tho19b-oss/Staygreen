using System;
using System.Drawing;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Abbrechbarer Countdown vor dem Herunterfahren. OK = jetzt herunterfahren (oder Zeit abgelaufen),
    /// Abbrechen = Rechner bleibt an.
    /// </summary>
    sealed class CountdownForm : Form
    {
        readonly WrapLabel _text = new WrapLabel { ForeColor = Theme.Text };
        readonly FlatButton _now = new FlatButton { Kind = ButtonKind.Secondary, MinWidth = 150 };
        readonly FlatButton _cancel = new FlatButton { Kind = ButtonKind.Primary, MinWidth = 150 };
        readonly Timer _timer = new Timer { Interval = 1000 };
        int _remaining;

        public CountdownForm(int seconds)
        {
            _remaining = seconds;

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

            _now.Text = Loc.T("cd.now");
            _now.DialogResult = DialogResult.OK;
            _cancel.Text = Loc.T("cd.cancel");
            _cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(_text);
            Controls.Add(_now);
            Controls.Add(_cancel);
            CancelButton = _cancel;
            AcceptButton = _cancel; // Enter bricht ab: lieber zu vorsichtig als versehentlich herunterfahren

            UpdateText();
            ArrangeControls();

            _timer.Tick += (o, e) =>
            {
                _remaining--;
                if (_remaining <= 0)
                {
                    _timer.Stop();
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                UpdateText();
            };
        }

        void ArrangeControls()
        {
            int margin = Dpi.Px(20);
            int width = Dpi.Px(460);
            int inner = width - 2 * margin;

            int textHeight = Math.Max(_text.MeasureHeight(inner), Dpi.Px(48));
            _text.SetBounds(margin, margin, inner, textHeight);

            int gap = Dpi.Px(12);
            int buttonWidth = (inner - gap) / 2;
            int buttonHeight = _now.Height;
            int y = margin + textHeight + Dpi.Px(16);
            _now.SetBounds(margin, y, buttonWidth, buttonHeight);
            _cancel.SetBounds(margin + buttonWidth + gap, y, inner - buttonWidth - gap, buttonHeight);

            ClientSize = new Size(width, y + buttonHeight + margin);
        }

        void UpdateText()
        {
            _text.Text = Loc.T("cd.text", _remaining);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _timer.Start();
            Activate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
