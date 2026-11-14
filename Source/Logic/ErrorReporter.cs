using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SirCelShading
{
    // Rassemble les erreurs du greffon pour cette session de jeu en rapports
    // « erreur ».
    //
    // Chaque exception rattrapée et chaque ligne d'erreur que le greffon écrit
    // au journal du jeu arrive ici. La même erreur (même message, même haut de
    // pile) donne un seul rapport par session, avec le nombre de fois où elle
    // s'est produite et les dates de la première et de la dernière.
    //
    // L'enregistrement est rapide et ne touche aucun fichier : il peut avoir
    // lieu sur le fil de rendu. Tick, sur le fil principal, écrit les rapports
    // dans la file et envoie ce qui est prêt tant que le joueur est sur l'un de
    // nos serveurs. Une erreur attend SettleDelay avant de partir, pour que ses
    // répétitions soient comptées ; une fois envoyée, elle n'est plus que
    // comptée, jamais renvoyée pendant cette session.
    public sealed class ErrorReporter
    {
        public static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(30);

        // Les dernières lignes de journal du greffon, jointes à chaque rapport
        // comme contexte.
        public const int ContextLines = 20;

        // Erreurs distinctes gardées par session : au-delà, les nouvelles sont
        // abandonnées.
        public const int MaxErrors = 100;

        public const string MessageRemoved = "(ligne retirée : elle portait des données du joueur)";

        private sealed class Entry
        {
            public string FileName;
            public string Message;
            public string Stack;
            public DateTime First;
            public DateTime Last;
            public int Count;
            public List<string> Lines;
            public bool Dirty;
            public bool Sent;
        }

        private readonly object m_lock = new object();
        private readonly string m_plugin;
        private readonly string m_version;
        private readonly DateTime m_session;
        private readonly string m_sessionPrefix;
        private readonly ReportQueue m_queue;
        private readonly ReportScrubber m_scrubber;
        private readonly Func<DateTime> m_clock;
        private readonly Dictionary<string, Entry> m_entries = new Dictionary<string, Entry>();
        private readonly Dictionary<string, Entry> m_byFile = new Dictionary<string, Entry>();
        private readonly Queue<string> m_recent = new Queue<string>();

        public ErrorReporter(string plugin, string version, DateTime sessionUtc,
            ReportQueue queue, ReportScrubber scrubber, Func<DateTime> clock)
        {
            m_plugin = plugin ?? "";
            m_version = version ?? "";
            m_session = sessionUtc;
            m_sessionPrefix = SessionPrefix(sessionUtc);
            m_queue = queue;
            m_scrubber = scrubber ?? new ReportScrubber(null, null);
            m_clock = clock ?? (() => DateTime.UtcNow);
        }

        public ReportScrubber Scrubber
        {
            get { return m_scrubber; }
        }

        public ReportQueue Queue
        {
            get { return m_queue; }
        }

        public DateTime Session
        {
            get { return m_session; }
        }

        // Une ligne que le greffon a écrite au journal du jeu : gardée, nettoyée,
        // comme contexte des rapports suivants.
        public void Remember(string line)
        {
            try
            {
                foreach (var part in ReportScrubber.SplitLines(line))
                {
                    var clean = m_scrubber.Line(part);
                    if (string.IsNullOrEmpty(clean))
                        continue;
                    lock (m_lock)
                    {
                        m_recent.Enqueue(clean);
                        while (m_recent.Count > ContextLines)
                            m_recent.Dequeue();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // Une ligne d'erreur écrite au journal du jeu, sans exception.
        public void Record(string message)
        {
            Record(message, (string)null);
        }

        // Une exception rattrapée, avec ce que faisait le greffon.
        public void Record(string context, Exception error)
        {
            if (error == null)
            {
                Record(context, (string)null);
                return;
            }

            string message, stack;
            Describe(context, error, out message, out stack);
            Record(message, stack);
        }

        // Ne lève jamais d'exception : une faute ici ne doit pas s'ajouter à celle
        // qu'on rapporte.
        public void Record(string message, string stack)
        {
            try
            {
                var now = m_clock();
                var cleanMessage = FirstLine(m_scrubber.Text(message));
                if (cleanMessage.Length == 0)
                    cleanMessage = MessageRemoved;
                var cleanStack = m_scrubber.Text(stack);
                var key = cleanMessage + "\n" + TopOfStack(cleanStack);

                lock (m_lock)
                {
                    Entry entry;
                    if (m_entries.TryGetValue(key, out entry))
                    {
                        entry.Count++;
                        entry.Last = now;
                        if (!entry.Sent)
                            entry.Dirty = true;
                        return;
                    }

                    if (m_entries.Count >= MaxErrors)
                        return;

                    entry = new Entry
                    {
                        FileName = m_sessionPrefix + "-" + ErrorReport.KindError + "-" + Hash(key) + ReportQueue.Extension,
                        Message = cleanMessage,
                        Stack = cleanStack,
                        First = now,
                        Last = now,
                        Count = 1,
                        Lines = new List<string>(m_recent),
                        Dirty = true,
                    };
                    m_entries[key] = entry;
                    m_byFile[entry.FileName] = entry;
                }
            }
            catch (Exception)
            {
            }
        }

        // Un rapport fabriqué ailleurs (le plantage de la session précédente),
        // mis directement dans la file.
        public bool Enqueue(ErrorReport report)
        {
            try
            {
                var name = SessionPrefix(report.Session) + "-" + report.Kind + "-"
                    + Hash(report.Message + "\n" + report.Stack) + ReportQueue.Extension;
                return m_queue.Put(name, report.ToUtf8());
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Les rapports de cette session pas encore envoyés, tels qu'ils sont.
        public List<ErrorReport> Pending()
        {
            var reports = new List<ErrorReport>();
            lock (m_lock)
            {
                foreach (var entry in m_entries.Values)
                {
                    if (!entry.Sent)
                        reports.Add(ToReport(entry));
                }
            }
            return reports;
        }

        // Fil principal, environ une fois par seconde : écrit les rapports
        // modifiés dans la file, puis envoie ce qui est prêt si le joueur est sur
        // l'un de nos serveurs. Rend le nombre de rapports partis. Ne lève jamais
        // d'exception.
        public int Tick(bool onOurServer, Func<byte[], bool> send)
        {
            try
            {
                Persist();
                if (!onOurServer)
                    return 0;

                var sent = m_queue.Flush(true, Ready, send);
                lock (m_lock)
                {
                    foreach (var name in sent)
                    {
                        Entry entry;
                        if (m_byFile.TryGetValue(name, out entry))
                        {
                            entry.Sent = true;
                            entry.Dirty = false;
                        }
                    }
                }
                return sent.Count;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // Écrit chaque rapport modifié dans la file : à chaque tour, et à la
        // sortie propre du jeu. Ne lève jamais d'exception.
        public void Persist()
        {
            try
            {
                var toWrite = new List<KeyValuePair<string, ErrorReport>>();
                lock (m_lock)
                {
                    foreach (var entry in m_entries.Values)
                    {
                        if (!entry.Dirty || entry.Sent)
                            continue;
                        entry.Dirty = false;
                        toWrite.Add(new KeyValuePair<string, ErrorReport>(entry.FileName, ToReport(entry)));
                    }
                }

                foreach (var item in toWrite)
                {
                    if (!m_queue.Put(item.Key, item.Value.ToUtf8()))
                    {
                        lock (m_lock)
                        {
                            Entry entry;
                            if (m_byFile.TryGetValue(item.Key, out entry) && !entry.Sent)
                                entry.Dirty = true;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // Les rapports des sessions précédentes partent aussitôt ; ceux de cette
        // session quand SettleDelay est écoulé depuis leur première occurrence.
        private bool Ready(string name)
        {
            if (!name.StartsWith(m_sessionPrefix, StringComparison.Ordinal))
                return true;
            lock (m_lock)
            {
                Entry entry;
                if (!m_byFile.TryGetValue(name, out entry))
                    return true;
                return m_clock() - entry.First >= SettleDelay;
            }
        }

        private ErrorReport ToReport(Entry entry)
        {
            return new ErrorReport
            {
                Plugin = m_plugin,
                Version = m_version,
                Kind = ErrorReport.KindError,
                Message = entry.Message,
                Stack = entry.Stack,
                First = entry.First,
                Last = entry.Last,
                Count = entry.Count,
                Session = m_session,
                Lines = new List<string>(entry.Lines),
            };
        }

        // « contexte : Type : message » pour le message, le reste de
        // l'exception (exceptions internes, cadres) pour la pile.
        public static void Describe(string context, Exception error, out string message, out string stack)
        {
            var lines = ReportScrubber.SplitLines(error.ToString());
            var head = lines.Length > 0 ? lines[0] : error.GetType().FullName;
            message = string.IsNullOrEmpty(context) ? head : context + ": " + head;

            var rest = new StringBuilder();
            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                    continue;
                if (rest.Length > 0)
                    rest.Append('\n');
                rest.Append(lines[i]);
            }
            stack = rest.ToString();
        }

        // Le premier cadre d'une pile : ce qui distingue deux erreurs de même
        // message.
        public static string TopOfStack(string stack)
        {
            string first = null;
            foreach (var line in ReportScrubber.SplitLines(stack))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;
                if (trimmed.StartsWith("at ", StringComparison.Ordinal))
                    return trimmed;
                if (first == null)
                    first = trimmed;
            }
            return first ?? "";
        }

        public static string SessionPrefix(DateTime sessionUtc)
        {
            var utc = sessionUtc.Kind == DateTimeKind.Local ? sessionUtc.ToUniversalTime() : sessionUtc;
            return utc.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        }

        private static string FirstLine(string text)
        {
            foreach (var line in ReportScrubber.SplitLines(text))
            {
                if (line.Trim().Length > 0)
                    return line.Trim();
            }
            return "";
        }

        // FNV-1a 64 : le même partout et d'un lancement à l'autre
        // (string.GetHashCode ne l'est pas).
        public static string Hash(string text)
        {
            unchecked
            {
                var hash = 14695981039346656037UL;
                foreach (var c in text ?? "")
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
    }
}
