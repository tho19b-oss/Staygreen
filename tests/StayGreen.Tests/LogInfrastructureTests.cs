using System;
using System.IO;
using System.Linq;
using System.Text;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class LogInfrastructureTests : IDisposable
    {
        readonly string _dir = Path.Combine(Path.GetTempPath(), "staygreen-tests-" + Guid.NewGuid().ToString("N"));

        public LogInfrastructureTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        // ---- LogFile ----

        [Fact]
        public void Append_CreatesFoldersAndWritesTheBomOnlyOnce()
        {
            string path = Path.Combine(_dir, "a", "b", "log.txt");
            LogFile.Append(path, "eins\r\n", 1000);
            LogFile.Append(path, "zwei\r\n", 1000);

            byte[] bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            string text = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
            Assert.Equal("eins\r\nzwei\r\n", text);
        }

        [Fact]
        public void Append_RotatesWhenTheFileGetsTooBig_KeepingOneBackup()
        {
            string path = Path.Combine(_dir, "log.txt");
            string line = new string('x', 40) + "\r\n";

            for (int i = 0; i < 12; i++) LogFile.Append(path, line, 200);

            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path + ".1"));
            Assert.True(new FileInfo(path).Length < 400);           // die laufende Datei bleibt klein
            Assert.False(File.Exists(path + ".2"));                 // nur eine Sicherung

            // Es geht nichts verloren, solange die Datei nicht mehr als einmal gedreht wurde.
            int lines = File.ReadAllLines(path).Length + File.ReadAllLines(path + ".1").Length;
            Assert.True(lines >= 6);
        }

        [Fact]
        public void Append_WithoutLimit_NeverRotates()
        {
            string path = Path.Combine(_dir, "log.txt");
            for (int i = 0; i < 20; i++) LogFile.Append(path, new string('x', 100) + "\r\n", 0);
            Assert.False(File.Exists(path + ".1"));
        }

        [Fact]
        public void Append_UnwritablePath_Throws_SoTheCallerCanReportIt()
        {
            string blocker = Path.Combine(_dir, "blocker");
            File.WriteAllText(blocker, "datei");
            Assert.ThrowsAny<Exception>(() => LogFile.Append(Path.Combine(blocker, "log.txt"), "x", 100));
        }

        [Fact]
        public void ProtocolLog_RotatesLargeFiles()
        {
            string path = Path.Combine(_dir, "protokoll.txt");
            File.WriteAllText(path, new string('x', (int)LogFile.ProtocolMaxBytes + 10));    // schon zu gross
            var s = new Settings { LogEnabled = true, LogPath = path };
            new ProtocolLog(s, () => "unused").Write(new DateTime(2026, 10, 1, 8, 0, 0), "START", "Manuell");

            Assert.True(File.Exists(path + ".1"));
            Assert.Contains("START", File.ReadAllText(path));
            Assert.True(new FileInfo(path).Length < 200);
        }

        // ---- ErrorLog ----

        [Fact]
        public void ErrorLog_WritesTheFirstErrorInFull()
        {
            string path = Path.Combine(_dir, "error.log");
            var log = new ErrorLog(() => path, 100000);
            log.Write(new DateTime(2026, 10, 1, 8, 0, 0), "System.Exception: kaputt\r\n   bei Foo.Bar()");
            string text = File.ReadAllText(path);
            Assert.Contains("2026-10-01 08:00:00  System.Exception: kaputt", text);
            Assert.Contains("bei Foo.Bar()", text);
        }

        [Fact]
        public void ErrorLog_SummarizesRepeatsInsteadOfFloodingTheFile()
        {
            string path = Path.Combine(_dir, "error.log");
            var log = new ErrorLog(() => path, 100000);
            DateTime t = new DateTime(2026, 10, 1, 8, 0, 0);

            for (int i = 0; i < 500; i++) log.Write(t.AddSeconds(i), "immer derselbe Fehler");   // jede Sekunde, unter 10 Minuten
            Assert.Equal(499, log.Suppressed);
            Assert.Single(File.ReadAllLines(path), l => l.Contains("immer derselbe Fehler"));

            log.Write(t.AddSeconds(500), "ein anderer Fehler");
            string text = File.ReadAllText(path);
            Assert.Contains("noch 499 Mal aufgetreten", text);
            Assert.Contains("ein anderer Fehler", text);
            Assert.Equal(0, log.Suppressed);
        }

        [Fact]
        public void ErrorLog_WritesTheSameErrorAgainAfterTheRepeatWindow()
        {
            string path = Path.Combine(_dir, "error.log");
            var log = new ErrorLog(() => path, 100000);
            DateTime t = new DateTime(2026, 10, 1, 8, 0, 0);
            log.Write(t, "Fehler");
            log.Write(t.AddMinutes(1), "Fehler");
            log.Write(t.Add(ErrorLog.RepeatWindow).AddSeconds(1), "Fehler");

            string text = File.ReadAllText(path);
            Assert.Equal(2, text.Split(new[] { "  Fehler" }, StringSplitOptions.None).Length - 1);
            Assert.Contains("noch 1 Mal aufgetreten", text);
        }

        [Fact]
        public void ErrorLog_ClockMovedBackwards_DoesNotSwallowTheError()
        {
            string path = Path.Combine(_dir, "error.log");
            var log = new ErrorLog(() => path, 100000);
            DateTime t = new DateTime(2026, 10, 1, 8, 0, 0);
            log.Write(t, "Fehler");
            log.Write(t.AddHours(-1), "Fehler");                      // Uhr zurueckgestellt: nicht als Wiederholung zaehlen
            Assert.Equal(2, File.ReadAllText(path).Split(new[] { "  Fehler" }, StringSplitOptions.None).Length - 1);
        }

        [Fact]
        public void ErrorLog_NeverThrows_EvenIfThePathIsBroken()
        {
            string blocker = Path.Combine(_dir, "blocker");
            File.WriteAllText(blocker, "datei");
            var log = new ErrorLog(() => Path.Combine(blocker, "error.log"), 1000);
            log.Write(DateTime.Now, "egal");                          // darf nicht werfen
        }

        [Fact]
        public void ErrorLog_IsLimitedInSize()
        {
            string path = Path.Combine(_dir, "error.log");
            var log = new ErrorLog(() => path, 2000);
            DateTime t = new DateTime(2026, 10, 1, 8, 0, 0);
            for (int i = 0; i < 100; i++) log.Write(t.AddSeconds(i), "Fehler Nummer " + i + new string('y', 200));
            Assert.True(new FileInfo(path).Length < 3000);
            Assert.True(File.Exists(path + ".1"));
        }

        // ---- AtomicFile ----

        [Fact]
        public void AtomicFile_WritesANewFile()
        {
            string path = Path.Combine(_dir, "sub", "settings.ini");
            AtomicFile.WriteAllText(path, "a=1", new UTF8Encoding(false));
            Assert.Equal("a=1", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp"));
        }

        [Fact]
        public void AtomicFile_ReplacesAnExistingFile_WithoutLeavingATempFile()
        {
            string path = Path.Combine(_dir, "settings.ini");
            AtomicFile.WriteAllText(path, "alt", new UTF8Encoding(false));
            AtomicFile.WriteAllText(path, "neu", new UTF8Encoding(false));
            AtomicFile.WriteAllText(path, "neuer", new UTF8Encoding(false));
            Assert.Equal("neuer", File.ReadAllText(path));
            Assert.Equal(new[] { "settings.ini" }, Directory.GetFiles(_dir).Select(Path.GetFileName).ToArray());
        }

        [Fact]
        public void AtomicFile_UnwritableTarget_Throws()
        {
            string blocker = Path.Combine(_dir, "blocker");
            File.WriteAllText(blocker, "datei");
            Assert.ThrowsAny<Exception>(() => AtomicFile.WriteAllText(Path.Combine(blocker, "x.ini"), "a", new UTF8Encoding(false)));
        }
    }
}
