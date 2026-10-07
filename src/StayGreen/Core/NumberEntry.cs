using System;
using System.Globalization;

namespace StayGreen.Core
{
    /// <summary>
    /// Eingabelogik eines Zahlenfelds ohne Oberflaeche (damit sie sich testen laesst): eine ganze Zahl zwischen
    /// <see cref="Minimum"/> und <see cref="Maximum"/>, die man in Schritten aendert oder tippt. Getippte Ziffern sind ein
    /// Entwurf: Die erste ersetzt die Zahl, weitere haengen sich an. Ueber dem Maximum rueckt der Entwurf sofort darauf;
    /// unter dem Minimum darf er waehrend des Tippens bleiben (fuer "30" tippt man zuerst "3"). <see cref="Value"/> liefert
    /// ihn schon in den Grenzen, <see cref="Commit"/> uebernimmt ihn und beendet das Tippen.
    /// </summary>
    public sealed class NumberEntry
    {
        int _value;
        string _draft;   // null = es wird nicht getippt

        public NumberEntry(int minimum, int maximum)
        {
            if (minimum < 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (maximum < minimum) throw new ArgumentOutOfRangeException(nameof(maximum));
            Minimum = minimum;
            Maximum = maximum;
            _value = minimum;
        }

        public int Minimum { get; }

        public int Maximum { get; }

        /// <summary>Hoechstens so viele Ziffern lassen sich tippen, wie das Maximum hat.</summary>
        public int MaxDigits
        {
            get { return Format(Maximum).Length; }
        }

        /// <summary>Es wird getippt: Weitere Ziffern haengen sich an.</summary>
        public bool IsTyping
        {
            get { return _draft != null; }
        }

        /// <summary>Was im Feld steht: beim Tippen der Entwurf (auch leer), sonst die Zahl.</summary>
        public string Text
        {
            get { return _draft ?? Format(_value); }
        }

        /// <summary>Die Zahl, die gilt: beim Tippen der Entwurf in den Grenzen, solange er leer ist die Zahl von vorher.</summary>
        public int Value
        {
            get { return string.IsNullOrEmpty(_draft) ? _value : Clamp(long.Parse(_draft, CultureInfo.InvariantCulture)); }
        }

        /// <summary>Setzt die Zahl (in den Grenzen) und verwirft einen Entwurf.</summary>
        public void SetValue(int value)
        {
            _draft = null;
            _value = Clamp(value);
        }

        /// <summary>
        /// Eine getippte Ziffer ersetzt die Zahl oder haengt sich an den Entwurf; fuehrende Nullen fallen weg. Mehr Ziffern,
        /// als das Maximum hat, nimmt das Feld nicht an. Andere Zeichen bleiben ohne Wirkung.
        /// </summary>
        public void Type(char digit)
        {
            if (digit < '0' || digit > '9') return;
            string draft = ((_draft ?? "") + digit).TrimStart('0');
            if (draft.Length == 0) draft = "0";
            if (draft.Length > MaxDigits) return;
            if (long.Parse(draft, CultureInfo.InvariantCulture) > Maximum) draft = Format(Maximum);
            _draft = draft;
        }

        /// <summary>Loescht die letzte getippte Ziffer; ist noch nichts getippt, die ganze Zahl (wie bei markiertem Text).</summary>
        public void Backspace()
        {
            if (_draft == null) _draft = "";
            else if (_draft.Length > 0) _draft = _draft.Substring(0, _draft.Length - 1);
        }

        /// <summary>Leert das Feld, um neu zu tippen.</summary>
        public void Clear()
        {
            _draft = "";
        }

        /// <summary>Uebernimmt den Entwurf und beendet das Tippen; ein leerer Entwurf laesst die Zahl, wie sie war.</summary>
        public void Commit()
        {
            _value = Value;
            _draft = null;
        }

        /// <summary>Uebernimmt einen Entwurf und aendert die Zahl dann um <paramref name="delta"/>; an den Grenzen bleibt sie stehen.</summary>
        public void Step(int delta)
        {
            Commit();
            _value = Clamp((long)_value + delta);
        }

        /// <summary>Ob ein Schritt in diese Richtung die Zahl aendern wuerde (am Rand des Bereichs nicht).</summary>
        public bool CanStep(int delta)
        {
            int value = Value;
            if (delta > 0) return value < Maximum;
            return delta < 0 && value > Minimum;
        }

        int Clamp(long value)
        {
            return (int)Math.Min(Math.Max(value, Minimum), Maximum);
        }

        static string Format(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
