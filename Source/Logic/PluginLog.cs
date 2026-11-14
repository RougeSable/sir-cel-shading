using System;

namespace SirCelShading
{
    // Les lignes du greffon dans le journal du jeu, toutes préfixées par
    // [sir-cel-shading].
    //
    // Une ligne d'erreur, avec ou sans exception rattrapée, va aussi au
    // rapporteur d'erreurs : chaque erreur que le greffon écrit au journal du
    // jeu donne un rapport « erreur ». Les lignes simples sont gardées comme
    // contexte des rapports.
    //
    // Appelable depuis n'importe quel fil, ne lève jamais d'exception.
    public sealed class PluginLog
    {
        public const string ErrorMark = "ERREUR : ";

        private readonly string m_prefix;
        private readonly Action<string> m_write;
        private volatile ErrorReporter m_reporter;

        public PluginLog(string id, Action<string> write, ErrorReporter reporter)
        {
            m_prefix = "[" + id + "] ";
            m_write = write ?? (_ => { });
            m_reporter = reporter;
        }

        public string Prefix
        {
            get { return m_prefix; }
        }

        public ErrorReporter Reporter
        {
            get { return m_reporter; }
            set { m_reporter = value; }
        }

        public void Info(string text)
        {
            var line = m_prefix + text;
            Write(line);
            var reporter = m_reporter;
            if (reporter != null)
                reporter.Remember(line);
        }

        public void Error(string text)
        {
            Error(text, null);
        }

        public void Error(string text, Exception error)
        {
            var line = m_prefix + ErrorMark + text + (error == null ? "" : ": " + error);
            Write(line);
            var reporter = m_reporter;
            if (reporter == null)
                return;
            reporter.Record(text, error);
            reporter.Remember(m_prefix + ErrorMark + text);
        }

        private void Write(string line)
        {
            try
            {
                m_write(line);
            }
            catch (Exception)
            {
            }
        }
    }
}
