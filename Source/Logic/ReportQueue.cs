using System;
using System.Collections.Generic;
using System.IO;

namespace SirCelShading
{
    // Les rapports qui attendent de partir, un fichier chacun dans le dossier
    // du greffon (Storage\sir-cel-shading\Reports). Ils ne partent que tant que
    // le joueur est sur l'un des serveurs du studio ; ailleurs ils attendent,
    // d'un lancement du jeu à l'autre, la prochaine connexion à l'un d'eux.
    //
    // Rien ici ne doit jamais casser le jeu : une faute en écrivant, en lisant
    // ou en envoyant est avalée, le rapport reste où il est et le prochain essai
    // a lieu plus tard.
    public sealed class ReportQueue
    {
        public const string Extension = ".json";

        // Au-delà, les rapports les plus anciens sont abandonnés : la file ne
        // remplit jamais le disque du joueur.
        public const int MaxFiles = 100;

        // Au plus ce nombre de rapports part par appel : jamais une rafale de
        // messages à la connexion.
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

        // Écrit (ou réécrit) un rapport. Faux s'il n'a pas pu être écrit.
        public bool Put(string name, byte[] content)
        {
            try
            {
                System.IO.Directory.CreateDirectory(m_directory);
                var path = Path.Combine(m_directory, name);

                // Écrit à côté du fichier, puis mis à sa place : un plantage du jeu
                // ne laisse jamais la moitié d'un rapport.
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

        // Les rapports en attente, du plus ancien au plus récent (leur nom
        // commence par la session).
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

        // Envoie les rapports en attente qui sont prêts, seulement sur l'un de
        // nos serveurs. Un rapport quitte le dossier une fois envoyé. Rend les
        // noms envoyés. Un envoi raté arrête ce tour : le rapport reste et sera
        // retenté plus tard.
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
                // L'envoi a levé une exception : le rapport reste pour le prochain tour.
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
