using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Zahlenfeld im Stil der Karten (Intervall, Mausweg, Vorwarnung). Ersetzt das Windows-Zahlenfeld, das im dunklen
    /// Design hell blieb. Mit dem Fokus ist die Zahl gruen markiert wie der gewaehlte Teil im Uhrzeitfeld: Getippte Ziffern
    /// ersetzen sie, weitere haengen sich an (ein Strich dahinter zeigt das), die Ruecktaste loescht. Pfeil hoch/runter,
    /// die kleinen Pfeile rechts und das Mausrad aendern um 1, Bild hoch/runter um 10, Pos1 und Ende springen an die
    /// Grenzen. Was getippt ist, gilt sofort (<see cref="Value"/>, in die Grenzen gerueckt) und steht so im Feld, sobald es
    /// den Fokus verliert, ein Pfeil benutzt oder auf die Zahl geklickt wird. Die Regeln dafuer stehen in
    /// <see cref="NumberEntry"/>.
    /// </summary>
    sealed class NumberBox : SpinField
    {
        /// <summary>Schritt fuer Bild hoch/runter.</summary>
        const int LargeStep = 10;

        readonly NumberEntry _entry;
        readonly Timer _blink = new Timer();
        readonly bool _blinks;
        bool _caretVisible;

        public NumberBox(int minimum, int maximum)
        {
            _entry = new NumberEntry(minimum, maximum);

            // Der Strich blinkt im Takt von Windows; ist das Blinken dort abgeschaltet (-1), steht er still.
            int blinkTime = SystemInformation.CaretBlinkTime;
            _blinks = blinkTime > 0;
            _blink.Interval = _blinks ? blinkTime : 500;
            _blink.Tick += (o, e) =>
            {
                _caretVisible = !_caretVisible;
                Invalidate();
            };
            Fit();
        }

        /// <summary>Die Zahl hat sich geaendert, auch schon beim Tippen.</summary>
        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return _entry.Minimum; }
        }

        public int Maximum
        {
            get { return _entry.Maximum; }
        }

        /// <summary>Die Zahl; beim Tippen schon das Getippte, in die Grenzen gerueckt. Auch ein gesetzter Wert rueckt in die Grenzen.</summary>
        public int Value
        {
            get { return _entry.Value; }
            set { Change(() => _entry.SetValue(value)); }
        }

        /// <summary>Fuehrt eine Aenderung aus, zeichnet neu und meldet eine geaenderte Zahl (auch an Screenreader).</summary>
        void Change(Action action)
        {
            int before = _entry.Value;
            action();
            RestartCaret();
            Invalidate();
            if (_entry.Value == before) return;
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            EventHandler handler = ValueChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>Der Strich hinter den Ziffern steht nur beim Tippen und ist nach jeder Eingabe sofort zu sehen.</summary>
        void RestartCaret()
        {
            _blink.Stop();
            _caretVisible = Focused && _entry.IsTyping;
            if (_caretVisible && _blinks) _blink.Start();
        }

        protected override void Step(int delta)
        {
            Change(() => _entry.Step(delta));
        }

        protected override bool CanStep(int delta)
        {
            return _entry.CanStep(delta);
        }

        // ------------------------------------------------------------------ Tastatur und Maus

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;
            switch (e.KeyData)
            {
                case Keys.Up:
                    Step(1);
                    break;
                case Keys.Down:
                    Step(-1);
                    break;
                case Keys.PageUp:
                    Step(LargeStep);
                    break;
                case Keys.PageDown:
                    Step(-LargeStep);
                    break;
                case Keys.Home:
                    Value = Minimum;
                    break;
                case Keys.End:
                    Value = Maximum;
                    break;
                case Keys.Back:
                    Change(_entry.Backspace);
                    break;
                case Keys.Delete:
                    Change(_entry.Clear);
                    break;
                default:
                    return;
            }
            e.Handled = true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.Handled) return;
            char c = e.KeyChar;
            if (c < '0' || c > '9') return;
            Change(() => _entry.Type(c));
            e.Handled = true;
        }

        /// <summary>Ein Klick auf die Zahl uebernimmt, was getippt ist, und markiert sie wieder (wie ein Doppelklick in einem Textfeld).</summary>
        protected override void OnContentClick(Point location)
        {
            Change(_entry.Commit);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            RestartCaret();
        }

        /// <summary>Beim Verlassen gilt, was getippt ist; im Feld steht dann die Zahl in den Grenzen.</summary>
        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Change(_entry.Commit);
        }

        // ------------------------------------------------------------------ Groesse und Zeichnen

        protected override int ContentWidth
        {
            get { return Metrics.TextWidth(new string('0', _entry.MaxDigits), Font); }
        }

        protected override void PaintContent(Graphics g, Color fore)
        {
            string text = _entry.Text;
            int width = Metrics.TextWidth(text, Font);
            var bounds = new Rectangle(TextLeft, 0, Math.Max(1, ArrowBounds.Left - TextLeft), Height);
            if (Focused && !_entry.IsTyping)
            {
                // Markiert: Was jetzt getippt wird, ersetzt die Zahl.
                int height = Font.Height + Dpi.Px(2);
                var mark = new Rectangle(TextLeft - Dpi.Px(2), (Height - height) / 2, width + Dpi.Px(4), height);
                Metrics.PaintRounded(g, mark, Dpi.Px(3), Theme.GreenButton, Color.Empty);
                fore = Theme.OnAccent;
            }
            TextRenderer.DrawText(g, text, Font, bounds, fore, TextFlags);

            if (!_caretVisible) return;
            int caretHeight = Font.Height;
            int x = TextLeft + width + (width > 0 ? Dpi.Px(1) : 0);
            using (var brush = new SolidBrush(fore))
                g.FillRectangle(brush, x, (Height - caretHeight) / 2, Math.Max(1, SystemInformation.CaretWidth), caretHeight);
        }

        // ------------------------------------------------------------------ Screenreader

        /// <summary>Screenreader lesen die Zahl (beim Tippen das Getippte) und koennen sie auch setzen.</summary>
        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new NumberBoxAccessibleObject(this);
        }

        sealed class NumberBoxAccessibleObject : ControlAccessibleObject
        {
            readonly NumberBox _owner;

            public NumberBoxAccessibleObject(NumberBox owner)
                : base(owner)
            {
                _owner = owner;
            }

            public override string Value
            {
                get { return _owner._entry.Text; }
                set
                {
                    int number;
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) _owner.Value = number;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _blink.Dispose();
            base.Dispose(disposing);
        }
    }
}
