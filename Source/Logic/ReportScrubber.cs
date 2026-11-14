using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SirCelShading
{
    // Retire tout ce qui concerne le joueur de ce qui entre dans un rapport.
    //
    // - Le dossier de l'utilisateur Windows devient %USERPROFILE%.
    // - Une ligne qui porte un identifiant Steam, un pseudo, un nom de grille
    //   ou une adresse de serveur est retirée en entier : une moitié de ligne
    //   pourrait encore trahir le reste.
    //
    // Une ligne est jugée sur ce qu'elle dit (les mots mêmes du jeu :
    // « UserId », « UserName », « Connecting to », « Grid name »...), sur sa
    // forme (un identifiant Steam, une adresse IP, un lien steam://) et sur les
    // noms connus de cette machine (le pseudo et l'identifiant Steam du joueur,
    // le nom d'utilisateur Windows).
    public sealed class ReportScrubber
    {
        public const string UserProfile = "%USERPROFILE%";

        // Le dossier d'un utilisateur Windows (barres obliques ou inverses), et l'ancien
        // dossier de profil de Windows XP.
        private static readonly Regex ProfileFolder = new Regex(
            @"[A-Za-z]:[\\/]+(?:Users|Documents\sand\sSettings)[\\/]+[^\\/\s:*?""<>|]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Un identifiant Steam sur 64 bits (7656119...), et plus généralement tout
        // long nombre qui pourrait être un compte : aucun compteur du jeu dans un
        // journal n'est aussi long.
        private static readonly Regex LongNumber = new Regex(@"(?<!\d)\d{15,20}(?!\d)", RegexOptions.CultureInvariant);

        // Une adresse IPv4, avec ou sans son port. Les numéros de version ne sont
        // pas des adresses : les lignes qui parlent d'une version ou d'une
        // construction sont jugées sans elle.
        private static readonly Regex Address = new Regex(
            @"(?<![\d.])(?:25[0-5]|2[0-4]\d|1?\d?\d)(?:\.(?:25[0-5]|2[0-4]\d|1?\d?\d)){3}(?![\d.])",
            RegexOptions.CultureInvariant);

        private static readonly Regex VersionLine = new Regex(@"\b(?:version|build)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Les liens de connexion et les adresses IPv6 entre crochets.
        private static readonly Regex Link = new Regex(
            @"\b(?:steam|eos|xbl|ps|ip|tcp|udp)://|\[[0-9a-f]*:[0-9a-f:]{2,}\](?::\d+)?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Les mots du jeu et des greffons qui accompagnent des données du joueur.
        // Une grille ou un joueur est aussi attrapé dans un nom de type
        // (MyCubeGrid, MyPlayer), hors des piles d'appels.
        private static readonly Regex Words = new Regex(
            @"\w*(?:grid|grids|play[e]r|play[e]rs)\b|\b(?:user\s*id|user\s*name|online\s*name|online\s*user\s*id|steam\s*id|steam\s*user|persona\w*|nick\s*name|pseudo\w*|display\s*name|identity|logged\s+i[n]|ship\s*name|server|servers|server\s*name|host\s*name|hostname|lobby|connecting\s+[t]o|connected\s+[t]o|joining|joined|address|ip|public\s+ip|world\s*name|session\s*name|e-?mail|faction)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Une ligne de pile d'appels nomme du code, jamais des données : seuls ses
        // chemins et ses formes sont jugés, pas ses mots.
        private static readonly Regex StackFrame = new Regex(@"^\s*[a]t\s+[\w.<>`\[\],|+$]+\s*\(", RegexOptions.CultureInvariant);

        private readonly string m_profile;
        private readonly List<string> m_names = new List<string>();
        private readonly object m_lock = new object();

        // userProfile : le dossier de l'utilisateur Windows de cette machine (peut
        // être nul). names : le pseudo du joueur, son identifiant Steam, le nom
        // d'utilisateur Windows... Les noms de moins de trois caractères sont
        // ignorés : ils correspondraient à tout.
        public ReportScrubber(string userProfile, IEnumerable<string> names)
        {
            m_profile = string.IsNullOrEmpty(userProfile) ? null : userProfile.TrimEnd('\\', '/');
            if (names != null)
            {
                foreach (var name in names)
                    AddName(name);
            }
        }

        // Un nom appris plus tard (le pseudo du joueur une fois la partie ouverte).
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

        // La ligne à garder, ou nulle si elle doit être retirée.
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

        // Le nom comme mot entier : un utilisateur Windows nommé « User » ne doit
        // pas retirer toutes les lignes qui disent « UserDataPath ».
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

        // Un texte de plusieurs lignes, chacune jugée pour elle-même.
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
