using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.FileSystem;
using VRage.Plugins;
using VRage.Utils;
using VRageRender;

namespace SirCelShading
{
    // Sir Cel Shading, greffon côté joueur chargé par Plugin Loader. Il ne fait
    // que le rendu bande dessinée, sur la machine du joueur : rien ne passe par
    // le serveur, un joueur sans le greffon voit le rendu du jeu.
    public class SirCelShadingPlugin : IPlugin
    {
        public const string Identifiant = "sir-cel-shading";

        // Tous les combien de mises à jour on regarde si un autre greffon s'est
        // accroché aux couleurs finales après nous (60 par seconde).
        private const int IntervalleDeCohabitation = 600;

        private Harmony m_harmony;
        private MoteurDeRendu m_moteur;
        private ArretDeSession m_arret;
        private string m_cheminReglages;
        private bool m_patchPose;
        private bool m_commandeBranchee;
        private int m_compteur;

        public static SirCelShadingPlugin Instance { get; private set; }

        public ArretDeSession Arret
        {
            get { return m_arret; }
        }

        public Reglages Reglages
        {
            get { return PassageCouleursFinales.ReglagesCourants.Copie(); }
        }

        public static void Journal(string texte)
        {
            MyLog.Default.WriteLine("[" + Identifiant + "] " + texte);
        }

        public void Init(object gameInstance)
        {
            Instance = this;
            m_arret = new ArretDeSession(Journal);
            PassageCouleursFinales.Arret = m_arret;

            // Dossier de réglages du joueur : %AppData%\SpaceEngineers\Storage\sir-cel-shading
            var dossier = Path.Combine(MyFileSystem.UserDataPath, "Storage", Identifiant);
            m_cheminReglages = Path.Combine(dossier, FichierReglages.NomDuFichier);

            string probleme;
            PassageCouleursFinales.ReglagesCourants = FichierReglages.Charger(m_cheminReglages, out probleme);
            if (probleme != null)
                Journal(probleme + " ; valeurs par défaut");

            Journal("chargé, rendu bande dessinée " + (PassageCouleursFinales.ReglagesCourants.Active ? "activé" : "coupé"));

            try
            {
                Preparer(dossier);
            }
            catch (Exception e)
            {
                m_arret.Arreter("préparation impossible : " + e, Textes.ArretPatch);
            }
        }

        private void Preparer(string dossier)
        {
            string manquant;
            m_moteur = MoteurDeRendu.Resoudre(out manquant);
            if (m_moteur == null)
            {
                m_arret.Arreter("moteur de rendu inattendu, introuvable : " + manquant, Textes.ArretMoteurInattendu);
                return;
            }
            PassageCouleursFinales.Moteur = m_moteur;

            var enTete = PassageCouleursFinales.EnTeteManquant(MyShaderCompiler.ShadersPath);
            if (enTete != null)
            {
                m_arret.Arreter("en-tête du jeu introuvable : " + enTete + " dans " + MyShaderCompiler.ShadersPath,
                    Textes.ArretEnTeteManquant);
                return;
            }

            var cheminShader = Path.Combine(dossier, "Shaders", SourceDuShader.NomDuFichier);
            try
            {
                EcrireShader(cheminShader);
            }
            catch (Exception e)
            {
                m_arret.Arreter("écriture de " + cheminShader + " impossible : " + e.Message, Textes.ArretFichierShader);
                return;
            }
            PassageCouleursFinales.CheminDuShader = cheminShader;

            // Deux greffons ne se disputent jamais une même étape du jeu.
            if (CederLaPlaceSiOccupe())
                return;

            m_harmony = new Harmony(Identifiant);
            var passage = typeof(PassageCouleursFinales);
            m_harmony.Patch(m_moteur.Run,
                prefix: new HarmonyMethod(passage.GetMethod("Prefix")),
                postfix: new HarmonyMethod(passage.GetMethod("Postfix")),
                finalizer: new HarmonyMethod(passage.GetMethod("Finalizer")));
            m_patchPose = true;
            Journal("branché sur MyToneMapping.Run, profondeur en t" + SourceDuShader.EmplacementProfondeur
                + ", effet écrit dans " + cheminShader);
        }

        private static void EcrireShader(string chemin)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(chemin));
            var texte = SourceDuShader.Texte;
            if (File.Exists(chemin) && File.ReadAllText(chemin) == texte)
                return;
            File.WriteAllText(chemin, texte);
        }

        // Vrai si un autre greffon est accroché aux couleurs finales : l'effet
        // s'arrête alors pour la session, et notre patch, s'il est posé, reste
        // inerte (le retirer pendant que le fil de rendu l'exécute risquerait de
        // laisser un champ du jeu remplacé).
        private bool CederLaPlaceSiOccupe()
        {
            var info = Harmony.GetPatchInfo(m_moteur.Run);
            var autres = Cohabitation.AutresProprietaires(info == null ? null : info.Owners, Identifiant);
            if (autres.Count == 0)
                return false;

            var noms = string.Join(", ", autres.ToArray());
            m_arret.Arreter("MyToneMapping.Run est déjà patché par " + noms + " : on cède la place",
                string.Format(Textes.ArretCohabitation, noms));
            return true;
        }

        public void Update()
        {
            try
            {
                var partieOuverte = MyAPIGateway.Session != null && MyAPIGateway.Utilities != null;

                if (!m_commandeBranchee && MyAPIGateway.Utilities != null)
                {
                    MyAPIGateway.Utilities.MessageEntered += SurMessage;
                    m_commandeBranchee = true;
                }

                m_arret.Delivrer(partieOuverte, Notifier);

                if (m_patchPose && !m_arret.EstArrete && ++m_compteur >= IntervalleDeCohabitation)
                {
                    m_compteur = 0;
                    CederLaPlaceSiOccupe();
                }
            }
            catch (Exception e)
            {
                // Une panne ici ne doit jamais faire tomber le jeu.
                m_arret.Arreter("anomalie sur le fil principal : " + e, Textes.ArretAnomalie);
            }
        }

        private static void Notifier(string message)
        {
            var rouge = message.StartsWith(Textes.ArretPrefixe, StringComparison.Ordinal);
            MyAPIGateway.Utilities.ShowNotification(message, rouge ? 10000 : 3000, rouge ? "Red" : "White");
        }

        private void SurMessage(string texte, ref bool envoyerAuxAutres)
        {
            var action = CommandeCel.Interpreter(texte);
            if (action == ActionCel.Aucune)
                return;

            // La commande reste sur la machine du joueur.
            envoyerAuxAutres = false;

            var reglages = Reglages;
            switch (action)
            {
                case ActionCel.Basculer:
                    reglages.Active = !reglages.Active;
                    break;
                case ActionCel.Activer:
                    reglages.Active = true;
                    break;
                case ActionCel.Couper:
                    reglages.Active = false;
                    break;
                case ActionCel.Etat:
                    m_arret.Prevenir(Textes.Etat(reglages.Active, m_arret.Raison));
                    return;
                default:
                    m_arret.Prevenir(Textes.AideCommande);
                    return;
            }

            Appliquer(reglages);
            m_arret.Prevenir(Textes.Etat(reglages.Active, m_arret.Raison));
        }

        // Prise en compte immédiate, sans relancer le jeu : l'image suivante
        // est dessinée avec les nouveaux réglages.
        public void Appliquer(Reglages reglages)
        {
            PassageCouleursFinales.ReglagesCourants = reglages;
            try
            {
                FichierReglages.Enregistrer(m_cheminReglages, PassageCouleursFinales.ReglagesCourants);
            }
            catch (Exception e)
            {
                Journal("réglages non enregistrés : " + e.Message);
            }
        }

        // Appelé par Plugin Loader, bouton des réglages du greffon.
        public void OpenConfigDialog()
        {
            MyGuiSandbox.AddScreen(new EcranReglages(this));
        }

        public void Dispose()
        {
            try
            {
                if (m_commandeBranchee && MyAPIGateway.Utilities != null)
                    MyAPIGateway.Utilities.MessageEntered -= SurMessage;
                m_commandeBranchee = false;

                // Seulement nos patchs, jamais ceux des autres.
                if (m_harmony != null)
                    m_harmony.UnpatchAll(Identifiant);
            }
            catch (Exception e)
            {
                Journal("arrêt incomplet : " + e.Message);
            }
            Instance = null;
        }
    }
}
