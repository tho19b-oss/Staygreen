using System;
using System.Globalization;
using System.Windows.Forms;
using StayGreen.Core;
using StayGreen.Platform;
using StayGreen.UI;

namespace StayGreen
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            CommandLine cmd = CommandLine.Parse(args);

            if (cmd.Help)
            {
                // Die Einstellungsdatei wird hier bewusst nicht gelesen: --lang oder die Windows-Sprache genuegen.
                Loc.Language = Loc.Resolve(cmd.Language ?? "auto", CultureInfo.CurrentUICulture);
                MessageBox.Show(Loc.T("cli.help"), Loc.T("app.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            SettingsStore.UseFile(cmd.SettingsPath);

            if (cmd.SelfTestPath != null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                int code = SelfTest.Run(cmd.SelfTestPath);
                if (cmd.SelfTestInteractive) SelfTest.ShowReport(cmd.SelfTestPath, code == 0);
                return code;
            }

            using (var instance = new SingleInstance())
            {
                if (!instance.IsFirst)
                {
                    // Schon gestartet: Gab es einen Befehl (--start, --stop, --toggle, --pause, --resume), geht er an die
                    // laufende Instanz, sonst (oder wenn keine antwortet, z. B. eine aeltere Version) wird ihr Fenster
                    // nach vorn geholt. Danach beendet sich dieser Start still.
                    bool delivered = cmd.Remote != null && CommandPipe.Send(cmd.Remote.ToLine());
                    if (!delivered) instance.SignalFirstInstance();
                    return 0;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Fehler in Ereignisbehandlern nicht zum Absturz werden lassen, sondern festhalten und weiterlaufen.
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (sender, e) => CrashLog.Write(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => CrashLog.Write(e.ExceptionObject as Exception);

                using (var app = new AppController(cmd, instance))
                    Application.Run(app);
            }
            return 0;
        }
    }
}
