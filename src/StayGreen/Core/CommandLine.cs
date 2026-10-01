using System;
using System.IO;

namespace StayGreen.Core
{
    /// <summary>Kommandozeile, z. B. fuer Verknuepfungen: "StayGreen.exe --start --minimized".</summary>
    public sealed class CommandLine
    {
        /// <summary>Vom Windows-Autostart gestartet (leise: kein Fenster).</summary>
        public bool Autostart { get; private set; }

        /// <summary>Ohne Fenster starten (nur Infobereich).</summary>
        public bool Minimized { get; private set; }

        /// <summary>Aktivhalten sofort starten (unabhaengig von der Einstellung).</summary>
        public bool Start { get; private set; }

        /// <summary>Aktivhalten nicht automatisch starten (unabhaengig von der Einstellung).</summary>
        public bool NoStart { get; private set; }

        public bool Help { get; private set; }

        /// <summary>Selbsttest ausfuehren und das Ergebnis in diese Datei schreiben.</summary>
        public string SelfTestPath { get; private set; }

        /// <summary>Andere Einstellungsdatei verwenden (z. B. fuer mehrere Profile oder USB-Stick).</summary>
        public string SettingsPath { get; private set; }

        /// <summary>"de" oder "en": Sprache nur fuer diesen Start.</summary>
        public string Language { get; private set; }

        public static readonly string HelpText =
            "StayGreen [Optionen]\r\n\r\n"
            + "  --start            Aktivhalten sofort starten\r\n"
            + "  --no-start         Aktivhalten nicht automatisch starten\r\n"
            + "  --minimized        Nur im Infobereich starten (ohne Fenster)\r\n"
            + "  --settings <Datei> Andere Einstellungsdatei verwenden\r\n"
            + "  --lang de|en       Sprache fuer diesen Start\r\n"
            + "  --selftest [Datei] Selbsttest ausfuehren und Ergebnis speichern\r\n"
            + "  --autostart        (intern) Start durch den Windows-Autostart\r\n"
            + "  --help             Diese Hilfe\r\n";

        public static string DefaultSelfTestPath
        {
            get { return Path.Combine(Path.GetTempPath(), "staygreen-selftest.txt"); }
        }

        public static CommandLine Parse(string[] args)
        {
            var result = new CommandLine();
            if (args == null) return result;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = (args[i] ?? "").Trim();
                if (arg.Length == 0) continue;

                string key = arg.ToLowerInvariant();
                string value = null;
                int eq = arg.IndexOf('=');
                if (arg.StartsWith("--") && eq > 2)
                {
                    key = arg.Substring(0, eq).ToLowerInvariant();
                    value = arg.Substring(eq + 1);
                }

                switch (key)
                {
                    case "--autostart":
                        result.Autostart = true;
                        break;
                    case "--minimized":
                    case "-m":
                        result.Minimized = true;
                        break;
                    case "--start":
                        result.Start = true;
                        break;
                    case "--no-start":
                        result.NoStart = true;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        result.Help = true;
                        break;
                    case "--selftest":
                        result.SelfTestPath = value ?? NextValue(args, ref i) ?? DefaultSelfTestPath;
                        break;
                    case "--settings":
                        result.SettingsPath = value ?? NextValue(args, ref i);
                        break;
                    case "--lang":
                        string lang = (value ?? NextValue(args, ref i) ?? "").Trim().ToLowerInvariant();
                        if (lang == "de" || lang == "en") result.Language = lang;
                        break;
                }
            }
            return result;
        }

        /// <summary>Naechstes Argument als Wert, sofern es keine Option ist.</summary>
        static string NextValue(string[] args, ref int index)
        {
            if (index + 1 < args.Length && args[index + 1] != null && !args[index + 1].StartsWith("-"))
            {
                index++;
                return args[index];
            }
            return null;
        }
    }
}
