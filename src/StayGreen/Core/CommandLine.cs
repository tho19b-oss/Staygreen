using System;
using System.Globalization;
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

        /// <summary>"--stop": laeuft schon eine Instanz, wird sie gestoppt; sonst startet StayGreen ohne Aktivhalten.</summary>
        public bool Stop { get; private set; }

        /// <summary>"--toggle": laeuft schon eine Instanz, wird umgeschaltet; sonst startet StayGreen mit Aktivhalten.</summary>
        public bool Toggle { get; private set; }

        /// <summary>
        /// Der Befehl, der einer bereits laufenden Instanz geschickt wird (--start, --stop, --toggle, --pause, --resume);
        /// null, wenn keiner angegeben ist (dann wird nur das Fenster nach vorn geholt).
        /// </summary>
        public RemoteCommand Remote { get; private set; }

        public bool Help { get; private set; }

        /// <summary>Selbsttest ausfuehren und das Ergebnis in diese Datei schreiben.</summary>
        public string SelfTestPath { get; private set; }

        /// <summary>Selbsttest ohne Dateiangabe (vom Nutzer gestartet): Ergebnis zusaetzlich in einem Fenster zeigen.</summary>
        public bool SelfTestInteractive { get; private set; }

        /// <summary>Andere Einstellungsdatei verwenden (z. B. fuer mehrere Profile oder USB-Stick).</summary>
        public string SettingsPath { get; private set; }

        /// <summary>"de" oder "en": Sprache nur fuer diesen Start.</summary>
        public string Language { get; private set; }

        /// <summary>"light", "dark" oder "auto": Darstellung nur fuer diesen Start; null = Einstellung gilt.</summary>
        public string Theme { get; private set; }

        /// <summary>Seite, die beim Start gezeigt wird (0 Aktivitaet, 1 Zeitplan inkl. Auto-Stopp, 2 System); -1 = Standard.</summary>
        public int Tab { get; private set; } = -1;

        /// <summary>
        /// Soll StayGreen beim Start das Aktivhalten beginnen (unabhaengig von der Einstellung)? "--pause" gehoert
        /// nicht dazu: Eine Pause wirkt nur auf ein laufendes Aktivhalten.
        /// </summary>
        public bool StartRequested
        {
            get { return Start || Toggle; }
        }

        /// <summary>Soll StayGreen beim Start ausdruecklich NICHT mit dem Aktivhalten beginnen?</summary>
        public bool NoStartRequested
        {
            get { return NoStart || Stop; }
        }

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
                        result.Remote = new RemoteCommand(RemoteAction.Start);
                        break;
                    case "--no-start":
                        result.NoStart = true;
                        break;
                    case "--stop":
                        result.Stop = true;
                        result.Remote = new RemoteCommand(RemoteAction.Stop);
                        break;
                    case "--toggle":
                        result.Toggle = true;
                        result.Remote = new RemoteCommand(RemoteAction.Toggle);
                        break;
                    case "--resume":
                        result.Remote = new RemoteCommand(RemoteAction.Resume);
                        break;
                    case "--pause":
                        result.Remote = new RemoteCommand(RemoteAction.Pause, ParseMinutes(value ?? NextNumber(args, ref i)));
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        result.Help = true;
                        break;
                    case "--selftest":
                        string path = value ?? NextValue(args, ref i);
                        result.SelfTestInteractive = path == null;
                        result.SelfTestPath = path ?? DefaultSelfTestPath;
                        break;
                    case "--settings":
                        result.SettingsPath = value ?? NextValue(args, ref i);
                        break;
                    case "--tab":
                        result.Tab = ParseTab(value ?? NextValue(args, ref i));
                        break;
                    case "--lang":
                        string lang = (value ?? NextValue(args, ref i) ?? "").Trim().ToLowerInvariant();
                        if (lang == "de" || lang == "en") result.Language = lang;
                        break;
                    case "--theme":
                        string theme = (value ?? NextValue(args, ref i) ?? "").Trim().ToLowerInvariant();
                        if (theme == "light" || theme == "dark" || theme == "auto") result.Theme = theme;
                        break;
                }
            }
            return result;
        }

        static int ParseMinutes(string text)
        {
            int minutes;
            if (!int.TryParse((text ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out minutes)) minutes = 0;
            return RemoteCommand.ClampMinutes(minutes);
        }

        static int ParseTab(string text)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                // Auto-Stopp liegt seit Version 1.1 auf der Seite "Zeitplan"; die alten Namen und Nummern zeigen dorthin.
                case "0": case "activity": case "aktivitaet": case "aktivität": return 0;
                case "1": case "2": case "schedule": case "zeitplan":
                case "stop": case "autostop": case "auto-stop": case "auto-stopp": case "autostopp": return 1;
                case "3": case "system": return 2;
                default: return -1;
            }
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

        /// <summary>Naechstes Argument, aber nur wenn es eine Zahl ist (z. B. "--pause 45"); sonst bleibt es unberuehrt.</summary>
        static string NextNumber(string[] args, ref int index)
        {
            int ignored;
            if (index + 1 < args.Length && args[index + 1] != null
                && int.TryParse(args[index + 1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out ignored))
            {
                index++;
                return args[index];
            }
            return null;
        }
    }
}
