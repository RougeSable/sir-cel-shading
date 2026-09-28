using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SirCelShading
{
    // Le seul chemin par lequel l'effet s'arrête : nom introuvable dans le
    // moteur de rendu, variante refusée par le compilateur du jeu, anomalie sur
    // le fil de rendu, étape déjà occupée par un autre greffon.
    //
    // Un arrêt vaut pour toute la session de jeu : l'effet ne revient pas tout
    // seul, le rendu du jeu reprend, une ligne part au journal du jeu et le
    // joueur est prévenu par une notification dès qu'une partie est ouverte.
    //
    // Appelable depuis n'importe quel fil : le fil de rendu s'arrête ici, le fil
    // principal délivre les notifications.
    public sealed class ArretDeSession
    {
        private readonly Action<string> m_journal;
        private readonly ConcurrentQueue<string> m_notifications = new ConcurrentQueue<string>();
        private int m_arrete;
        private volatile string m_raison;

        public ArretDeSession(Action<string> journal)
        {
            m_journal = journal ?? (_ => { });
        }

        public bool EstArrete
        {
            get { return Volatile.Read(ref m_arrete) != 0; }
        }

        // Ce qui a arrêté l'effet, dans les mots donnés au joueur.
        public string Raison
        {
            get { return m_raison; }
        }

        // Vrai au premier arrêt seulement : les suivants ne répètent ni la
        // ligne de journal ni la notification.
        public bool Arreter(string detailPourLeJournal, string messagePourLeJoueur)
        {
            if (Interlocked.Exchange(ref m_arrete, 1) != 0)
                return false;

            m_raison = messagePourLeJoueur;
            m_journal(detailPourLeJournal);
            m_notifications.Enqueue(messagePourLeJoueur);
            return true;
        }

        // Une information au joueur qui n'arrête rien (bascule, par exemple).
        public void Prevenir(string messagePourLeJoueur)
        {
            m_notifications.Enqueue(messagePourLeJoueur);
        }

        // Délivre ce qui attend, mais seulement quand une partie est ouverte :
        // au menu principal, les messages patientent.
        public int Delivrer(bool partieOuverte, Action<string> afficher)
        {
            if (!partieOuverte || afficher == null)
                return 0;

            var nombre = 0;
            string message;
            while (m_notifications.TryDequeue(out message))
            {
                afficher(message);
                nombre++;
            }
            return nombre;
        }
    }
}
