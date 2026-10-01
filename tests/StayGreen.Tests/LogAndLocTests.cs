using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    public class FormatTests
    {
        [Fact]
        public void Duration_FormatsHoursWithoutDayOverflow()
        {
            Assert.Equal("00:00:00", Format.Duration(TimeSpan.Zero));
            Assert.Equal("00:05:30", Format.Duration(new TimeSpan(0, 5, 30)));
            Assert.Equal("26:03:04", Format.Duration(new TimeSpan(1, 2, 3, 4)));
            Assert.Equal("00:00:00", Format.Duration(TimeSpan.FromSeconds(-5)));
        }
    }

    public class ProtocolLogTests : IDisposable
    {
        readonly string _dir = Path.Combine(Path.GetTempPath(), "staygreen-tests-" + Guid.NewGuid().ToString("N"));

        public ProtocolLogTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        [Fact]
        public void FormatLine_IsStable()
        {
            Assert.Equal("2026-10-01 08:03:12  START    Manuell",
                ProtocolLog.FormatLine(new DateTime(2026, 10, 1, 8, 3, 12), "START", "Manuell"));
            Assert.Equal("2026-10-01 08:03:12  AUTOSTOP Ende",
                ProtocolLog.FormatLine(new DateTime(2026, 10, 1, 8, 3, 12), "AUTOSTOP", "Ende"));
        }

        [Fact]
        public void Disabled_WritesNothing()
        {
            var s = new Settings { LogEnabled = false, LogPath = Path.Combine(_dir, "a.txt") };
            new ProtocolLog(s, () => Path.Combine(_dir, "default.txt")).Write(DateTime.Now, "START", "x");
            Assert.False(File.Exists(s.LogPath));
            Assert.False(File.Exists(Path.Combine(_dir, "default.txt")));
        }

        [Fact]
        public void Enabled_AppendsLinesInOrder()
        {
            string path = Path.Combine(_dir, "sub", "protokoll.txt");   // Ordner wird angelegt
            var s = new Settings { LogEnabled = true, LogPath = path };
            var log = new ProtocolLog(s, () => "unused");
            log.Write(new DateTime(2026, 10, 1, 8, 0, 0), "START", "Manuell");
            log.Write(new DateTime(2026, 10, 1, 17, 0, 0), "STOP", "Laufzeit 09:00:00");

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.StartsWith("2026-10-01 08:00:00  START", lines[0]);
            Assert.StartsWith("2026-10-01 17:00:00  STOP", lines[1]);
            Assert.Null(log.LastError);
        }

        [Fact]
        public void EmptyPath_UsesDefaultPath()
        {
            string def = Path.Combine(_dir, "default.txt");
            var s = new Settings { LogEnabled = true, LogPath = "   " };
            var log = new ProtocolLog(s, () => def);
            Assert.Equal(def, log.CurrentPath);
            log.Write(DateTime.Now, "START", "x");
            Assert.True(File.Exists(def));
        }

        [Fact]
        public void UnwritablePath_DoesNotThrow_AndReportsError()
        {
            string blocker = Path.Combine(_dir, "blocker");
            File.WriteAllText(blocker, "ich bin eine Datei, kein Ordner");
            var s = new Settings { LogEnabled = true, LogPath = Path.Combine(blocker, "log.txt") };
            var log = new ProtocolLog(s, () => "unused");
            log.Write(DateTime.Now, "START", "x");      // darf nicht werfen
            Assert.NotNull(log.LastError);
        }

        [Fact]
        public void Umlauts_AreStoredAsUtf8()
        {
            string path = Path.Combine(_dir, "u.txt");
            var s = new Settings { LogEnabled = true, LogPath = path };
            new ProtocolLog(s, () => "unused").Write(DateTime.Now, "STOP", "Müller schließt Fenster – fertig");
            Assert.Contains("Müller schließt Fenster – fertig", File.ReadAllText(path));
        }
    }

    public class LocTests
    {
        static readonly Regex Placeholder = new Regex(@"\{(\d+)\}");

        [Fact]
        public void EveryKey_HasBothLanguages_AndNoDuplicates()
        {
            var seen = new HashSet<string>();
            foreach (string[] row in Loc.RawTable)
            {
                Assert.Equal(3, row.Length);
                Assert.False(string.IsNullOrWhiteSpace(row[0]), "leerer Schluessel");
                Assert.True(seen.Add(row[0]), "doppelter Schluessel: " + row[0]);
                Assert.False(string.IsNullOrWhiteSpace(row[1]), "Deutsch fehlt: " + row[0]);
                Assert.False(string.IsNullOrWhiteSpace(row[2]), "Englisch fehlt: " + row[0]);
            }
        }

        [Fact]
        public void Placeholders_MatchBetweenLanguages()
        {
            foreach (string[] row in Loc.RawTable)
            {
                var de = Placeholder.Matches(row[1]).Cast<Match>().Select(m => m.Value).OrderBy(x => x).ToArray();
                var en = Placeholder.Matches(row[2]).Cast<Match>().Select(m => m.Value).OrderBy(x => x).ToArray();
                Assert.True(de.SequenceEqual(en), "Platzhalter unterschiedlich bei " + row[0]);
            }
        }

        [Fact]
        public void Translations_FormatWithoutException()
        {
            string old = Loc.Language;
            try
            {
                foreach (string lang in new[] { "de", "en" })
                {
                    Loc.Language = lang;
                    foreach (string[] row in Loc.RawTable)
                    {
                        int max = Placeholder.Matches(row[lang == "de" ? 1 : 2]).Cast<Match>()
                            .Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(-1).Max();
                        object[] args = Enumerable.Range(0, max + 1).Select(i => (object)("x" + i)).ToArray();
                        Loc.T(row[0], args);   // wirft bei kaputten Klammern
                    }
                }
            }
            finally
            {
                Loc.Language = old;
            }
        }

        [Fact]
        public void Language_SwitchesTexts()
        {
            string old = Loc.Language;
            try
            {
                Loc.Language = "de";
                string de = Loc.T("log.runtime", "1");
                Loc.Language = "en";
                string en = Loc.T("log.runtime", "1");
                Assert.NotEqual(de, en);
                Loc.Language = "xx";                 // unbekannt -> Deutsch
                Assert.Equal("de", Loc.Language);
            }
            finally
            {
                Loc.Language = old;
            }
        }

        [Fact]
        public void MissingKey_IsVisibleNotSilent()
        {
            Assert.Equal("[does.not.exist]", Loc.T("does.not.exist"));
        }

        [Theory]
        [InlineData("auto", "de-DE", "de")]
        [InlineData("auto", "de-AT", "de")]
        [InlineData("auto", "en-US", "en")]
        [InlineData("auto", "fr-FR", "en")]
        [InlineData("de", "en-US", "de")]
        [InlineData("en", "de-DE", "en")]
        [InlineData("", "de-DE", "de")]
        public void Resolve_PicksLanguage(string setting, string culture, string expected)
        {
            Assert.Equal(expected, Loc.Resolve(setting, new CultureInfo(culture)));
        }

        [Fact]
        public void Days_AreDefinedForAllSevenDays()
        {
            for (int i = 0; i < 7; i++)
            {
                Assert.True(Loc.HasKey("day." + i + ".short"), "day." + i + ".short");
            }
        }

        [Fact]
        public void EveryKeyUsedInSourceCode_Exists()
        {
            string root = FindRepoRoot();
            Assert.NotNull(root);
            var used = new SortedSet<string>();

            // Jedes Textliteral der Form "praefix.name", dessen Praefix in der Tabelle vorkommt, gilt als Schluessel.
            // So werden auch Schluessel in Bedingungsausdruecken (ok ? "a.b" : "a.c") und Zuweisungen erfasst.
            var prefixes = new HashSet<string>(Loc.RawTable.Select(r => r[0].Split('.')[0]));
            var literal = new Regex(@"""([a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+)""");
            foreach (string file in Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
                if (file.EndsWith("Strings.cs")) continue;
                foreach (Match m in literal.Matches(File.ReadAllText(file)))
                {
                    string key = m.Groups[1].Value;
                    if (prefixes.Contains(key.Split('.')[0])) used.Add(key);
                }
            }

            Assert.NotEmpty(used);
            var missing = used.Where(k => !Loc.HasKey(k)).ToList();
            Assert.True(missing.Count == 0, "Fehlende Uebersetzungen: " + string.Join(", ", missing));
        }

        [Fact]
        public void NoUnusedKeys_InTranslationTable()
        {
            string root = FindRepoRoot();
            var all = string.Join("\n", Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                            && !f.EndsWith("Strings.cs"))
                .Select(File.ReadAllText));
            var unused = Loc.RawTable.Select(r => r[0])
                .Where(k => !k.StartsWith("day.") && !all.Contains("\"" + k + "\""))
                .ToList();
            Assert.True(unused.Count == 0, "Unbenutzte Schluessel: " + string.Join(", ", unused));
        }

        static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "StayGreen.sln"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
