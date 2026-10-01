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
        readonly Label _text = new Label();
        readonly Button _now = new Button();
        readonly Button _cancel = new Button();
        readonly Timer _timer = new Timer { Interval = 1000 };
        int _remaining;

        public CountdownForm(int seconds)
        {
            _remaining = seconds;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = Loc.T("cd.title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            ClientSize = new Size(430, 130);

            _text.SetBounds(16, 16, 398, 56);
            _text.AutoSize = false;
            _text.UseMnemonic = false;

            _now.Text = Loc.T("cd.now");
            _now.SetBounds(16, 84, 190, 32);
            _now.DialogResult = DialogResult.OK;

            _cancel.Text = Loc.T("cd.cancel");
            _cancel.SetBounds(224, 84, 190, 32);
            _cancel.DialogResult = DialogResult.Cancel;

            Controls.Add(_text);
            Controls.Add(_now);
            Controls.Add(_cancel);
            CancelButton = _cancel;
            AcceptButton = _cancel; // Enter bricht ab: lieber zu vorsichtig als versehentlich herunterfahren

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
            UpdateText();
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
