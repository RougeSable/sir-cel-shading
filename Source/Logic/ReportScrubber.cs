using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SirCelShading
{
    // Removes everything about the player from what goes into a report.
    //
    // - The Windows user folder becomes %USERPROFILE%.
    // - A line that carries a Steam ID, a player name, a grid name or a
    //   server address is removed as a whole: half a line could still give
    //   the rest away.
    //
    // A line is judged on what it says (the game's own wording: "UserId",
    // "UserName", "Connecting to", "Grid name"...), on its shape (a Steam ID,
    // an IP address, a steam:// link) and on the names known on this machine
    // (the player's name and Steam ID, the Windows user name).
    public sealed class ReportScrubber
    {
        public const string UserProfile = "%USERPROFILE%";

        // C:\Users\name or C:/Users/name, and the old Documents and Settings.
        private static readonly Regex ProfileFolder = new Regex(
            @"[A-Za-z]:[\\/]+(?:Users|Documents and Settings)[\\/]+[^\\/\s:*?""<>|]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // A 64-bit Steam ID (7656119...), and more generally any long number
        // that could be an account: no game counter in a log is that long.
        private static readonly Regex LongNumber = new Regex(@"(?<!\d)\d{15,20}(?!\d)", RegexOptions.CultureInvariant);

        // An IPv4 address, with or without its port. Version numbers are not
        // addresses: lines about a version or a build are judged without it.
        private static readonly Regex Address = new Regex(
            @"(?<![\d.])(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}(?![\d.])",
            RegexOptions.CultureInvariant);

        private static readonly Regex VersionLine = new Regex(@"\b(?:version|build)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Connection links and IPv6 addresses between brackets.
        private static readonly Regex Link = new Regex(
            @"\b(?:steam|eos|xbl|ps|ip|tcp|udp)://|\[[0-9a-f]*:[0-9a-f:]{2,}\](?::\d+)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // The words of the game and of the plugins next to player data. A
        // grid or a player is also caught inside a type name (MyCubeGrid,
        // MyPlayer), outside stack traces.
        private static readonly Regex Words = new Regex(
            @"\w*(?:grid|grids|player|players)\b|\b(?:user\s*id|user\s*name|online\s*name|online\s*user\s*id|steam\s*id|steam\s*user|persona\w*|nick\s*name|pseudo\w*|display\s*name|identity|logged\s+in|ship\s*name|server|servers|server\s*name|host\s*name|hostname|lobby|connecting\s+to|connected\s+to|joining|joined|address|ip|public\s+ip|world\s*name|session\s*name|e-?mail|faction)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // A line of a stack trace names code, never data: only its paths and
        // shapes are judged, not its words.
        private static readonly Regex StackFrame = new Regex(@"^\s*at\s+[\w.<>`\[\],|+$]+\s*\(", RegexOptions.CultureInvariant);

        private readonly string m_profile;
        private readonly List<string> m_names = new List<string>();
        private readonly object m_lock = new object();

        // userProfile: the Windows user folder of this machine (may be null).
        // names: the player's name, Steam ID, Windows user name... Names
        // shorter than three characters are ignored: they would match
        // everything.
        public ReportScrubber(string userProfile, IEnumerable<string> names)
        {
            m_profile = string.IsNullOrEmpty(userProfile) ? null : userProfile.TrimEnd('\\', '/');
            if (names != null)
            {
                foreach (var name in names)
                    AddName(name);
            }
        }

        // A name learnt later (the player's name once a game is open).
        public void AddName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;
            var trimmed = name.Trim();
            if (trimmed.Length < 3)
                return;
            lock (m_lock)
            {
                if (!m_names.Contains(trimmed))
                    m_names.Add(trimmed);
            }
        }

        // The line to keep, or null if it must be removed.
        public string Line(string line)
        {
            if (line == null)
                return null;

            var text = line;
            if (m_profile != null && m_profile.Length > 3)
                text = ReplaceIgnoringCase(text, m_profile, UserProfile);
            text = ProfileFolder.Replace(text, UserProfile);

            if (LongNumber.IsMatch(text) || Link.IsMatch(text))
                return null;
            if (Address.IsMatch(text) && !VersionLine.IsMatch(text))
                return null;
            if (!StackFrame.IsMatch(text) && Words.IsMatch(text))
                return null;

            lock (m_lock)
            {
                foreach (var name in m_names)
                {
                    if (ContainsWord(text, name))
                        return null;
                }
            }
            return text;
        }

        // The name as a whole word: a Windows user called "User" must not
        // remove every line that says "UserDataPath".
        private static bool ContainsWord(string text, string name)
        {
            var index = text.IndexOf(name, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                var end = index + name.Length;
                var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                var after = end >= text.Length || !char.IsLetterOrDigit(text[end]);
                if (before && after)
                    return true;
                index = text.IndexOf(name, index + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public List<string> Lines(IEnumerable<string> lines)
        {
            var kept = new List<string>();
            if (lines == null)
                return kept;
            foreach (var line in lines)
            {
                var clean = Line(line);
                if (clean != null)
                    kept.Add(clean);
            }
            return kept;
        }

        // A text of several lines, each judged on its own.
        public string Text(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return string.Join("\n", Lines(SplitLines(text)).ToArray());
        }

        public static string[] SplitLines(string text)
        {
            return (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static string ReplaceIgnoringCase(string text, string what, string by)
        {
            var index = text.IndexOf(what, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                text = text.Substring(0, index) + by + text.Substring(index + what.Length);
                index = text.IndexOf(what, index + by.Length, StringComparison.OrdinalIgnoreCase);
            }
            return text;
        }
    }
}
