using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SirCelShading
{
    // La seule façon dont l'effet s'arrête : un nom absent du moteur de rendu,
    // une variante refusée par le compilateur du jeu, une faute sur le fil de
    // rendu, une étape déjà utilisée par un autre greffon.
    //
    // Un arrêt dure toute la session de jeu : l'effet ne revient pas tout
    // seul, le rendu du jeu reprend, une ligne part au journal du jeu et le
    // joueur reçoit une notification dès qu'une partie est ouverte.
    //
    // Appelable depuis n'importe quel fil : le fil de rendu s'arrête ici, le
    // fil principal remet les notifications.
    public sealed class SessionStop
    {
        private readonly Action<string, Exception> m_log;
        private readonly ConcurrentQueue<string> m_notifications = new ConcurrentQueue<string>();
        private int m_stopped;
        private volatile string m_reason;

        public SessionStop(Action<string> log)
        {
            if (log == null)
                m_log = (detail, error) => { };
            else
                m_log = (detail, error) => log(error == null ? detail : detail + ": " + error);
        }

        private SessionStop(Action<string, Exception> log, bool unused)
        {
            m_log = log ?? ((detail, error) => { });
        }

        // Un arrêt dont la ligne de journal est une erreur du greffon
        // (PluginLog.Error) : il donne aussi un rapport « erreur », l'exception
        // gardée à part.
        public static SessionStop Logging(Action<string, Exception> logError)
        {
            return new SessionStop(logError, true);
        }

        public bool IsStopped
        {
            get { return Volatile.Read(ref m_stopped) != 0; }
        }

        // Ce qui a arrêté l'effet, dans les mots donnés au joueur.
        public string Reason
        {
            get { return m_reason; }
        }

        // Vrai au premier arrêt seulement : les suivants ne répètent ni la
        // ligne de journal ni la notification.
        public bool Stop(string detailForTheLog, string messageForThePlayer)
        {
            return Stop(detailForTheLog, messageForThePlayer, null);
        }

        // Pareil, avec l'exception rattrapée : elle va au journal après le
        // détail.
        public bool Stop(string detailForTheLog, string messageForThePlayer, Exception error)
        {
            if (Interlocked.Exchange(ref m_stopped, 1) != 0)
                return false;

            m_reason = messageForThePlayer;
            try
            {
                m_log(detailForTheLog, error);
            }
            catch (Exception)
            {
                // Écrire le journal ne doit pas empêcher l'arrêt.
            }
            m_notifications.Enqueue(messageForThePlayer);
            return true;
        }

        // Un message au joueur qui n'arrête rien (un interrupteur, par exemple).
        public void Inform(string messageForThePlayer)
        {
            m_notifications.Enqueue(messageForThePlayer);
        }

        // Remet ce qui attend, mais seulement quand une partie est ouverte :
        // dans le menu principal, les messages patientent.
        public int Deliver(bool gameOpen, Action<string> show)
        {
            if (!gameOpen || show == null)
                return 0;

            var count = 0;
            string message;
            while (m_notifications.TryDequeue(out message))
            {
                show(message);
                count++;
            }
            return count;
        }
    }
}
