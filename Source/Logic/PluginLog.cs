using System;

namespace SirCelShading
{
    // The plugin's lines in the game log, all prefixed with [sir-cel-shading].
    //
    // An error line, with or without a caught exception, also goes to the
    // error reporter: every error the plugin writes to the game log gives an
    // "erreur" report. Plain lines are kept as context for the reports.
    //
    // Callable from any thread, never throws.
    public sealed class PluginLog
    {
        public const string ErrorMark = "ERROR: ";

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
