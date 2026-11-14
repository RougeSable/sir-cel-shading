using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SirCelShading
{
    // Comment un plantage se reconnaît : une marque est posée dans le dossier
    // du greffon au début d'une session de jeu et effacée à sa sortie propre.
    // Une marque trouvée au démarrage suivant veut dire que cette session ne
    // s'est jamais terminée : le jeu a planté, ou il a été tué.
    //
    // La marque garde le début de la session (UTC) et le chemin de son journal
    // de jeu. Le greffon relit alors ce journal et construit un rapport
    // « plantage » : le bloc CRASH INFO du jeu, l'exception qui a fait tomber le
    // jeu avec sa pile, les 20 dernières lignes d'erreur du jeu avant la fin et
    // les 20 dernières lignes du greffon.
    public static class CrashWatch
    {
        public const string MarkerName = "session.marker";
        public const string GameLogPattern = "SpaceEngineers_*.log";
        public const int LastLines = 20;
        public const int MaxStackLines = 80;
        public const int MaxCrashInfoLines = 40;

        public const string NoException = "La session de jeu s'est terminée sans sortie propre (aucune exception dans le journal du jeu).";
        public const string CrashInfoHeading = "--- CRASH INFO ---";
        public const string GameErrorsHeading = "--- dernières lignes d'erreur du jeu ---";
        public const string PluginLinesHeading = "--- dernières lignes du greffon ---";

        public sealed class PreviousSession
        {
            public DateTime StartUtc;
            public string GameLog;
        }

        // Appelée au début d'une session. Rend la session restée inachevée, s'il
        // y en a une, puis pose la marque de cette session.
        public static PreviousSession Begin(string pluginDirectory, DateTime startUtc, string gameLog)
        {
            var path = Path.Combine(pluginDirectory, MarkerName);
            PreviousSession previous = null;
            try
            {
                if (File.Exists(path))
                    previous = ReadMarker(File.ReadAllLines(path));
            }
            catch (Exception)
            {
                previous = new PreviousSession();
            }

            try
            {
                Directory.CreateDirectory(pluginDirectory);
                File.WriteAllText(path,
                    ErrorReport.IsoUtc(startUtc) + "\n" + (gameLog ?? "") + "\n",
                    new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
            return previous;
        }

        // Appelée à la sortie propre du jeu.
        public static void End(string pluginDirectory)
        {
            try
            {
                var path = Path.Combine(pluginDirectory, MarkerName);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        public static bool HasMarker(string pluginDirectory)
        {
            try
            {
                return File.Exists(Path.Combine(pluginDirectory, MarkerName));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static PreviousSession ReadMarker(string[] lines)
        {
            var previous = new PreviousSession();
            if (lines.Length > 0)
            {
                DateTime start;
                if (DateTime.TryParse(lines[0].Trim(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out start))
                    previous.StartUtc = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            }
            if (lines.Length > 1 && lines[1].Trim().Length > 0)
                previous.GameLog = lines[1].Trim();
            return previous;
        }

        // Le journal de jeu de la session qui a planté : celui de la marque, ou
        // sinon le dernier SpaceEngineers_*.log du dossier autre que l'actuel
        // (leurs noms portent leur date).
        public static string FindGameLog(PreviousSession previous, string logDirectory, string currentLog)
        {
            try
            {
                if (previous != null && !string.IsNullOrEmpty(previous.GameLog) && File.Exists(previous.GameLog)
                    && !SamePath(previous.GameLog, currentLog))
                    return previous.GameLog;

                if (string.IsNullOrEmpty(logDirectory) || !Directory.Exists(logDirectory))
                    return null;

                var logs = new List<string>(Directory.GetFiles(logDirectory, GameLogPattern));
                logs.Sort(StringComparer.OrdinalIgnoreCase);
                for (var i = logs.Count - 1; i >= 0; i--)
                {
                    if (!SamePath(logs[i], currentLog))
                        return logs[i];
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return false;
            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Les lignes d'un journal de jeu que le jeu peut encore tenir ouvert.
        public static IEnumerable<string> ReadLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    yield return line;
            }
        }

        // Tout le début d'une session : la marque de la précédente, le rapport
        // de plantage si elle ne s'est jamais terminée, la marque de celle-ci.
        // Nul quand la session précédente s'est terminée proprement. Ne lève
        // jamais d'exception.
        public static ErrorReport Start(string pluginDirectory, string logDirectory, string currentLog,
            DateTime startUtc, string plugin, string version, string pluginTag, ReportScrubber scrubber)
        {
            var previous = Begin(pluginDirectory, startUtc, currentLog);
            if (previous == null)
                return null;

            try
            {
                var log = FindGameLog(previous, logDirectory, currentLog);
                IEnumerable<string> lines = log == null ? new string[0] : ReadLines(log);
                var lastWrite = log == null ? startUtc : File.GetLastWriteTimeUtc(log);
                return Build(previous, lines, lastWrite, plugin, version, pluginTag, scrubber);
            }
            catch (Exception)
            {
                return Build(previous, new string[0], startUtc, plugin, version, pluginTag, scrubber);
            }
        }

        // 2026-10-27 20:31:12.734 - Thread:   1 ->  text
        private static readonly Regex Stamped = new Regex(
            @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) - Thread:\s*\d+\s*->\s*(.*)$",
            RegexOptions.CultureInvariant);

        private static readonly Regex Timezone = new Regex(@"Timezone \(local - UTC\):\s*(-?\d+)h", RegexOptions.CultureInvariant);

        private static readonly Regex ErrorWords = new Regex(
            @"\b(?:error|errors|exception|critical|fatal|failed|failure)\b|ERROR:",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ExceptionLine = new Regex(
            @"Exception occur+ed|Unhandled exception|UnhandledException",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex StillRunning = new Regex(@"application\sis\scontinuing", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private sealed class ExceptionBlock
        {
            public string Message;
            public readonly List<string> Stack = new List<string>();
            public bool Continuing;
        }

        // Lit une seule fois le journal de jeu de la session plantée, du début à
        // la fin, et ne garde que ce dont le rapport a besoin : un journal de jeu
        // peut être volumineux.
        public static ErrorReport Build(PreviousSession previous, IEnumerable<string> logLines, DateTime lastWriteUtc,
            string plugin, string version, string pluginTag, ReportScrubber scrubber)
        {
            scrubber = scrubber ?? new ReportScrubber(null, null);
            var tag = "[" + pluginTag + "]";

            var gameErrors = new Queue<string>();
            var pluginLines = new Queue<string>();
            List<string> crashInfo = null;
            List<string> capturing = null;
            ExceptionBlock current = null;
            ExceptionBlock fatal = null;
            ExceptionBlock lastSevere = null;
            var timezoneHours = 0;
            DateTime? lastStamp = null;

            foreach (var raw in logLines ?? new string[0])
            {
                var line = raw ?? "";
                var stamped = Stamped.Match(line);
                if (!stamped.Success)
                {
                    // Une ligne d'une entrée sur plusieurs lignes : la pile d'une exception.
                    if (current != null && current.Stack.Count < MaxStackLines && line.Trim().Length > 0)
                        current.Stack.Add(line);
                    continue;
                }

                DateTime stamp;
                if (DateTime.TryParseExact(stamped.Groups[1].Value, "yyyy-MM-dd HH:mm:ss.fff",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out stamp))
                    lastStamp = stamp;

                var text = stamped.Groups[2].Value;
                current = null;

                var zone = Timezone.Match(text);
                if (zone.Success)
                    int.TryParse(zone.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out timezoneHours);

                if (capturing != null)
                {
                    capturing.Add(line);
                    if (text.IndexOf("OFNI HSARC", StringComparison.Ordinal) >= 0 || capturing.Count >= MaxCrashInfoLines)
                        capturing = null;
                    continue;
                }

                if (text.IndexOf("CRASH INFO", StringComparison.Ordinal) >= 0)
                {
                    crashInfo = new List<string> { line };
                    capturing = crashInfo;
                    // L'exception qui a fait tomber le jeu est la dernière avant le
                    // bloc de plantage.
                    fatal = lastSevere;
                    continue;
                }

                if (text.StartsWith(tag, StringComparison.Ordinal))
                {
                    Keep(pluginLines, line);
                    continue;
                }

                if (ExceptionLine.IsMatch(text))
                {
                    current = new ExceptionBlock { Message = text.Trim(), Continuing = StillRunning.IsMatch(text) };
                    if (!current.Continuing || lastSevere == null || lastSevere.Continuing)
                        lastSevere = current;
                }

                if (ErrorWords.IsMatch(text))
                    Keep(gameErrors, line);
            }

            if (fatal == null)
                fatal = lastSevere;

            var crashTime = lastStamp.HasValue
                ? DateTime.SpecifyKind(lastStamp.Value.AddHours(-timezoneHours), DateTimeKind.Utc)
                : DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc);

            var report = new ErrorReport
            {
                Plugin = plugin ?? "",
                Version = version ?? "",
                Kind = ErrorReport.KindCrash,
                First = crashTime,
                Last = crashTime,
                Count = 1,
                Session = previous != null && previous.StartUtc != default(DateTime) ? previous.StartUtc : crashTime,
            };

            var message = fatal == null ? "" : scrubber.Text(fatal.Message);
            report.Message = message.Length > 0 ? message : (fatal == null ? NoException : ErrorReporter.MessageRemoved);
            report.Stack = fatal == null ? "" : string.Join("\n", scrubber.Lines(fatal.Stack).ToArray());

            report.Lines.Add(CrashInfoHeading);
            report.Lines.AddRange(scrubber.Lines(crashInfo ?? new List<string>()));
            report.Lines.Add(GameErrorsHeading);
            report.Lines.AddRange(LastClean(gameErrors, scrubber));
            report.Lines.Add(PluginLinesHeading);
            report.Lines.AddRange(LastClean(pluginLines, scrubber));
            return report;
        }

        // On garde un peu plus de 20 lignes, pour qu'il en reste 20 une fois
        // retirées celles qui portent des données du joueur.
        private static void Keep(Queue<string> lines, string line)
        {
            lines.Enqueue(line);
            while (lines.Count > LastLines * 4)
                lines.Dequeue();
        }

        private static List<string> LastClean(IEnumerable<string> lines, ReportScrubber scrubber)
        {
            var clean = scrubber.Lines(lines);
            if (clean.Count > LastLines)
                clean.RemoveRange(0, clean.Count - LastLines);
            return clean;
        }
    }
}
