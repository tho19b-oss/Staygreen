using System;
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
                MessageBox.Show(CommandLine.HelpText, "StayGreen", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            SettingsStore.UseFile(cmd.SettingsPath);

            if (cmd.SelfTestPath != null)
                return SelfTest.Run(cmd.SelfTestPath);

            using (var instance = new SingleInstance())
            {
                if (!instance.IsFirst)
                {
                    // Schon gestartet: das vorhandene Fenster nach vorn holen und still beenden.
                    instance.SignalFirstInstance();
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
