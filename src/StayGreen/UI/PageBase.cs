using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;

namespace StayGreen.UI
{
    /// <summary>
    /// Gemeinsame Basis der Seiten (Aktivitaet, Zeitplan, System): bindet die Einstellungen, unterdrueckt
    /// Aenderungsereignisse beim Befuellen der Felder und stellt Bausteine fuer die Karten bereit.
    /// Eine Seite ist eine senkrechte Spalte aus Karten; gescrollt wird vom <see cref="ScrollHost"/> darum herum.
    /// </summary>
    abstract class PageBase : Stack
    {
        readonly List<EditableRow> _editables = new List<EditableRow>();
        bool _loading;

        protected PageBase()
        {
            Gap = 12;
        }

        protected Settings S { get; private set; }

        /// <summary>Wird ausgeloest, wenn der Nutzer etwas geaendert hat (nicht beim Befuellen der Felder).</summary>
        public event Action Changed;

        protected bool Loading
        {
            get { return _loading; }
        }

        protected void Fire()
        {
            if (!_loading && S != null && Changed != null) Changed();
        }

        /// <summary>Fuehrt <paramref name="action"/> aus, ohne dass Aenderungsereignisse als Nutzereingabe zaehlen.</summary>
        protected void Quiet(Action action)
        {
            bool previous = _loading;
            _loading = true;
            try { action(); }
            finally { _loading = previous; }
        }

        public void LoadSettings(Settings settings)
        {
            S = settings;
            CloseEditors(null);   // neu geladene Einstellungen: ein offener Entwurf passt nicht mehr dazu
            Quiet(() => Populate(settings));
        }

        /// <summary>Meldet eine aufklappbare Zeile an. Von allen angemeldeten Zeilen der Seite ist hoechstens ein Editor offen.</summary>
        protected EditableRow Track(EditableRow row)
        {
            _editables.Add(row);
            row.Opened += () => CloseEditors(row);
            return row;
        }

        /// <summary>Klappt alle Editoren der Seite zu, ausser <paramref name="keep"/>.</summary>
        protected void CloseEditors(EditableRow keep)
        {
            foreach (EditableRow row in _editables)
                if (row != keep) row.Close();
        }

        protected abstract void Populate(Settings settings);

        /// <summary>Setzt alle Beschriftungen in der aktuellen Sprache.</summary>
        public abstract void ApplyTexts();

        /// <summary>Jede Sekunde, solange die Seite sichtbar ist (fuer "naechster Start ..."-Zeilen).</summary>
        public virtual void Tick(DateTime now, DateTime? autoStopDue)
        {
        }

        /// <summary>Hoehe neu berechnen lassen (nach geaenderten Texten oder ein-/ausgeblendeten Zeilen).</summary>
        protected void RefreshLayout()
        {
            ScrollHost host = Host;
            if (host != null) host.Relayout();
        }

        /// <summary>Rollt die Seite, bis <paramref name="control"/> ganz zu sehen ist (soweit es in das Fenster passt).</summary>
        protected void ScrollIntoView(Control control)
        {
            ScrollHost host = Host;
            if (host != null) host.ScrollControlIntoView(control);
        }

        ScrollHost Host
        {
            get
            {
                for (Control c = Parent; c != null; c = c.Parent)
                {
                    var host = c as ScrollHost;
                    if (host != null) return host;
                }
                return null;
            }
        }

        // ---------------------------------------------------------------- Bausteine

        protected static SwitchBox CreateSwitch()
        {
            return new SwitchBox();
        }

        /// <summary>Faerbt ein Standard-Eingabefeld passend zum Design (im dunklen Design; sonst bestimmt Windows die Farben).</summary>
        protected static T Style<T>(T control) where T : Control
        {
            Theme.StyleInput(control);
            return control;
        }

        /// <summary>Datumsfeld im kurzen Datumsformat der Windows-Regionseinstellungen.</summary>
        protected static DateTimePicker CreateDatePicker()
        {
            return Style(new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Width = Dpi.Px(118),
            });
        }

        /// <summary>Voller Wochentagsname in der Sprache der Oberflaeche (Index 0 = Montag), z. B. fuer Screenreader.</summary>
        protected static string DayName(int index)
        {
            DayOfWeek day = (DayOfWeek)((index + 1) % 7);
            return new CultureInfo(Loc.Language).DateTimeFormat.GetDayName(day);
        }

        protected static Label CreateLabel()
        {
            return new Label { AutoSize = true, UseMnemonic = false, ForeColor = Theme.Muted };
        }

        protected static WrapLabel CreateHint()
        {
            return new WrapLabel { ForeColor = Theme.Muted };
        }

        /// <summary>Hinweis im aufgeklappten Editor: etwas kraeftiger als <see cref="CreateHint"/>, damit er auf der Toenung gut lesbar bleibt.</summary>
        protected static WrapLabel CreateEditorHint()
        {
            return new WrapLabel { ForeColor = Theme.MutedStrong };
        }

        /// <summary>Beschriftung im Editor (Einheit, "von"/"bis" ...), ebenfalls kraeftiger fuer die Toenung.</summary>
        protected static Label CreateEditorLabel()
        {
            return new Label { AutoSize = true, UseMnemonic = false, ForeColor = Theme.MutedStrong };
        }

        /// <summary>Kopf einer Karte: Titel, ein Satz Erklaerung und (optional) rechts der Schalter fuer die ganze Karte.</summary>
        protected static SettingRow CreateHeader(SwitchBox toggle)
        {
            return new SettingRow(toggle) { Heading = true, Inset = new Padding(0, 0, 0, 12) };
        }

        protected static FlatButton CreateButton(ButtonKind kind)
        {
            return new FlatButton { Kind = kind };
        }

        /// <summary>Ein Element mit etwas Luft darueber und darunter als eigene Zeile einer Karte.</summary>
        protected static Stack Pad(Control content, int top, int bottom)
        {
            var row = new Stack { Inset = new Padding(0, top, 0, bottom) };
            row.Controls.Add(content);
            return row;
        }
    }
}
