using System;
using System.Collections.Generic;
using System.IO;

namespace SirCelShading
{
    // The reports waiting to leave, one file each in the plugin's folder
    // (Storage\sir-cel-shading\Reports). They leave only while the player is
    // on one of the studio's servers; elsewhere they wait, across game
    // launches, for the next connection to one of them.
    //
    // Nothing here may ever break the game: a fault while writing, reading or
    // sending is swallowed, the report stays where it is and the next try
    // happens later.
    public sealed class ReportQueue
    {
        public const string Extension = ".json";

        // Beyond this, the oldest reports are dropped: the queue never fills
        // the player's disk.
        public const int MaxFiles = 100;

        // At most this many reports leave per call: never a burst of
        // messages on joining.
        public const int MaxSentPerFlush = 5;

        private readonly string m_directory;

        public ReportQueue(string directory)
        {
            m_directory = directory;
        }

        public string Directory
        {
            get { return m_directory; }
        }

        // Writes (or rewrites) a report. False if it could not be written.
        public bool Put(string name, byte[] content)
        {
            try
            {
                System.IO.Directory.CreateDirectory(m_directory);
                var path = Path.Combine(m_directory, name);

                // Written next to the file, then swapped in: a crash of the
                // game never leaves half a report.
                var temporary = path + ".tmp";
                File.WriteAllBytes(temporary, content);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temporary, path);

                Trim();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool Contains(string name)
        {
            try
            {
                return File.Exists(Path.Combine(m_directory, name));
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The waiting reports, oldest first (names start with the session).
        public List<string> Names()
        {
            var names = new List<string>();
            try
            {
                if (!System.IO.Directory.Exists(m_directory))
                    return names;
                foreach (var path in System.IO.Directory.GetFiles(m_directory, "*" + Extension))
                    names.Add(Path.GetFileName(path));
                names.Sort(StringComparer.Ordinal);
            }
            catch (Exception)
            {
            }
            return names;
        }

        public byte[] Read(string name)
        {
            try
            {
                return File.ReadAllBytes(Path.Combine(m_directory, name));
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Sends the waiting reports that are ready, only on one of our
        // servers. A report leaves the folder once sent. Returns the names
        // sent. A failed send stops this round: the report stays and is tried
        // again later.
        public List<string> Flush(bool onOurServer, Func<string, bool> ready, Func<byte[], bool> send)
        {
            var sent = new List<string>();
            if (!onOurServer || send == null)
                return sent;

            try
            {
                foreach (var name in Names())
                {
                    if (sent.Count >= MaxSentPerFlush)
                        break;
                    if (ready != null && !ready(name))
                        continue;

                    var content = Read(name);
                    if (content == null || content.Length == 0)
                    {
                        Delete(name);
                        continue;
                    }

                    if (!send(content))
                        break;

                    Delete(name);
                    sent.Add(name);
                }
            }
            catch (Exception)
            {
                // The send threw: the report stays for the next round.
            }
            return sent;
        }

        private void Delete(string name)
        {
            try
            {
                File.Delete(Path.Combine(m_directory, name));
            }
            catch (Exception)
            {
            }
        }

        private void Trim()
        {
            var names = Names();
            for (var i = 0; i < names.Count - MaxFiles; i++)
                Delete(names[i]);
        }
    }
}
