using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SirCelShading
{
    // How a crash is noticed: a marker is put in the plugin's folder when a
    // game session starts and removed when it ends cleanly. A marker found at
    // the next start means that session never ended: the game crashed, or
    // was killed.
    //
    // The marker keeps the start of the session (UTC) and the path of its
    // game log. The plugin then reads that log again and builds a "plantage"
    // report: the game's CRASH INFO block, the exception that brought the
    // game down with its stack, the last 20 error lines of the game before
    // the end and the last 20 lines of the plugin.
    public static class CrashWatch
    {
        public const string MarkerName = "session.marker";
        public const string GameLogPattern = "SpaceEngineers_*.log";
        public const int LastLines = 20;
        public const int MaxStackLines = 80;
        public const int MaxCrashInfoLines = 40;

        public const string NoException = "The game session ended without a clean exit (no exception in the game log).";
        public const string CrashInfoHeading = "--- CRASH INFO ---";
        public const string GameErrorsHeading = "--- last error lines of the game ---";
        public const string PluginLinesHeading = "--- last lines of the plugin ---";

        public sealed class PreviousSession
        {
            public DateTime StartUtc;
            public string GameLog;
        }

        // Called when a session starts. Returns the session left unfinished,
        // if any, then puts this session's marker.
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

        // Called at the clean exit of the game.
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

        // The game log of the session that crashed: the one in the marker, or
        // else the latest SpaceEngineers_*.log of the folder other than the
        // current one (their names carry their date).
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

        // The lines of a game log the game may still hold open.
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

        // The whole start of a session: the marker of the previous one, the
        // crash report if it never ended, this session's marker. Null when
        // the previous session ended cleanly. Never throws.
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

        private static readonly Regex StillRunning = new Regex(@"application is continuing", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private sealed class ExceptionBlock
        {
            public string Message;
            public readonly List<string> Stack = new List<string>();
            public bool Continuing;
        }

        // Reads the game log of the crashed session once, from start to end,
        // and keeps only what the report needs: a game log can be large.
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
                    // A line of a multi-line entry: the stack of an exception.
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
                    // The exception that brought the game down is the last one
                    // before the crash block.
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

        // Lines are kept a little beyond 20, so that 20 remain once those with
        // player data are removed.
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
