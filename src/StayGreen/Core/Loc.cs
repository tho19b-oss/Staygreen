using System;
using System.Collections.Generic;
using System.Globalization;

namespace StayGreen.Core
{
    /// <summary>
    /// Deutsch/Englisch ohne Ressourcendateien (die EXE bleibt eine einzelne Datei).
    /// Jede Zeile der Tabelle: Schluessel, Deutsch, Englisch.
    /// </summary>
    public static class Loc
    {
        static string _language = "de";
        static Dictionary<string, string> _de;
        static Dictionary<string, string> _en;

        /// <summary>"de" oder "en".</summary>
        public static string Language
        {
            get { return _language; }
            set { _language = value == "en" ? "en" : "de"; }
        }

        /// <summary>Loest "auto" anhand der Windows-Sprache auf.</summary>
        public static string Resolve(string setting, CultureInfo uiCulture)
        {
            if (setting == "de" || setting == "en") return setting;
            return uiCulture != null && uiCulture.TwoLetterISOLanguageName == "de" ? "de" : "en";
        }

        public static string T(string key)
        {
            EnsureLoaded();
            Dictionary<string, string> table = _language == "en" ? _en : _de;
            string value;
            return table.TryGetValue(key, out value) ? value : "[" + key + "]";
        }

        public static string T(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, T(key), args);
        }

        public static IEnumerable<string> Keys
        {
            get
            {
                EnsureLoaded();
                return _de.Keys;
            }
        }

        public static bool HasKey(string key)
        {
            EnsureLoaded();
            return _de.ContainsKey(key) && _en.ContainsKey(key);
        }

        /// <summary>Kurzer Wochentagsname, Index 0 = Montag.</summary>
        public static string DayShort(int index)
        {
            return T("day." + index + ".short");
        }

        public static string DayLong(int index)
        {
            return T("day." + index + ".long");
        }

        static void EnsureLoaded()
        {
            if (_de != null) return;
            var de = new Dictionary<string, string>();
            var en = new Dictionary<string, string>();
            foreach (string[] row in Table)
            {
                de[row[0]] = row[1];
                en[row[0]] = row[2];
            }
            _en = en;
            _de = de;
        }

        /// <summary>Fuer Tests: Rohtabelle.</summary>
        public static string[][] RawTable
        {
            get { return Table; }
        }

        static readonly string[][] Table = Strings.All;
    }
}
