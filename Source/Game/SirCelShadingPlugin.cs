using System;
using System.IO;
using HarmonyLib;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.FileSystem;
using VRage.Plugins;
using VRage.Utils;
using VRageRender;

namespace SirCelShading
{
    // Sir Cel Shading, un greffon côté joueur chargé par Pulsar. Il ne fait que
    // du cel shading, dans le style film d'animation, sur la machine du joueur :
    // un joueur sans le greffon voit le rendu du jeu. Son seul échange avec un
    // serveur est l'envoi de ses rapports d'erreurs et de plantages, sur les
    // serveurs du studio seulement (ErrorReporting).
    public class SirCelShadingPlugin : IPlugin
    {
        public const string Id = "sir-cel-shading";

        // Nombre de mises à jour entre deux vérifications qu'aucun autre greffon ne
        // s'est accroché aux couleurs finales après nous (60 par seconde).
        private const int CoexistenceInterval = 600;

        // Les lignes du greffon dans le journal du jeu ; les lignes d'erreur
        // donnent aussi des rapports.
        private static readonly PluginLog s_log = new PluginLog(Id, line => MyLog.Default.WriteLine(line), null);

        private Harmony m_harmony;
        private RenderEngine m_engine;
        private SessionStop m_stop;
        private ErrorReporting m_reporting;
        private string m_settingsDirectory;
        private bool m_patched;
        private bool m_commandHooked;
        private int m_counter;

        public static SirCelShadingPlugin Instance { get; private set; }

        public SessionStop Stop
        {
            get { return m_stop; }
        }

        public Settings Settings
        {
            get { return FinalColorsPass.CurrentSettings.Copy(); }
        }

        public static void Log(string text)
        {
            s_log.Info(text);
        }

        // Une ligne d'erreur dans le journal du jeu, qui donne aussi un rapport
        // d'erreur.
        public static void LogError(string text, Exception error = null)
        {
            s_log.Error(text, error);
        }

        public void Init(object gameInstance)
        {
            Instance = this;

            // Le dossier de réglages du joueur : %AppData%\SpaceEngineers\Storage\sir-cel-shading
            m_settingsDirectory = Path.Combine(MyFileSystem.UserDataPath, "Storage", Id);

            // D'abord : les erreurs de ce qui suit sont rapportées, et le plantage
            // de la session précédente, s'il y en a un, est mis en file.
            m_reporting = ErrorReporting.Start(m_settingsDirectory, Id);
            s_log.Reporter = m_reporting == null ? null : m_reporting.Reporter;

            m_stop = SessionStop.Logging((detail, error) => s_log.Error(detail, error));
            FinalColorsPass.Stop = m_stop;

            string problem;
            FinalColorsPass.CurrentSettings = SettingsFile.Load(m_settingsDirectory, out problem);
            if (problem != null)
                LogError(problem + " ; valeurs par défaut utilisées");

            var settings = FinalColorsPass.CurrentSettings;
            Log(Texts.DisplayName + " chargé, film d'animation, " + (settings.Enabled ? "activé" : "désactivé"));

            try
            {
                Prepare(m_settingsDirectory);
            }
            catch (Exception e)
            {
                m_stop.Stop("préparation échouée", Texts.StopPatch, e);
            }
        }

        private void Prepare(string directory)
        {
            string missing;
            m_engine = RenderEngine.Resolve(out missing);
            if (m_engine == null)
            {
                m_stop.Stop("moteur de rendu inattendu, introuvable : " + missing, Texts.StopUnexpectedEngine);
                return;
            }
            FinalColorsPass.Engine = m_engine;

            var header = FinalColorsPass.MissingHeader(MyShaderCompiler.ShadersPath);
            if (header != null)
            {
                m_stop.Stop("en-tête du jeu introuvable : " + header + " dans " + MyShaderCompiler.ShadersPath,
                    Texts.StopMissingHeader);
                return;
            }

            var shaderPath = Path.Combine(directory, "Shaders", ShaderSource.FileName);
            try
            {
                WriteShader(shaderPath);
            }
            catch (Exception e)
            {
                m_stop.Stop("écriture impossible de " + shaderPath, Texts.StopShaderFile, e);
                return;
            }
            FinalColorsPass.ShaderPath = shaderPath;

            // Deux greffons ne se disputent jamais une même étape du jeu.
            if (StepAsideIfTaken())
                return;

            m_harmony = new Harmony(Id);
            var pass = typeof(FinalColorsPass);
            m_harmony.Patch(m_engine.Run,
                prefix: new HarmonyMethod(pass.GetMethod("Prefix")),
                postfix: new HarmonyMethod(pass.GetMethod("Postfix")),
                finalizer: new HarmonyMethod(pass.GetMethod("Finalizer")));
            m_patched = true;
            Log("accroché à MyToneMapping.Run, profondeur en t" + ShaderSource.DepthSlot
                + ", albédo en t" + ShaderSource.AlbedoSlot + ", shader écrit dans " + shaderPath);
        }

        private static void WriteShader(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var text = ShaderSource.Text;
            if (File.Exists(path) && File.ReadAllText(path) == text)
                return;
            File.WriteAllText(path, text);
        }

        // Vrai si un autre greffon est accroché aux couleurs finales : l'effet
        // s'arrête alors pour la session, et notre correctif, s'il est posé,
        // reste inerte (le retirer pendant que le fil de rendu l'exécute pourrait
        // laisser un champ du jeu remplacé).
        private bool StepAsideIfTaken()
        {
            var info = Harmony.GetPatchInfo(m_engine.Run);
            var others = Coexistence.OtherOwners(info == null ? null : info.Owners, Id);
            if (others.Count == 0)
                return false;

            var names = string.Join(", ", others.ToArray());
            m_stop.Stop("MyToneMapping.Run est déjà modifié par " + names + " : on cède la place",
                string.Format(Texts.StopCoexistence, names));
            return true;
        }

        public void Update()
        {
            try
            {
                var gameOpen = MyAPIGateway.Session != null && MyAPIGateway.Utilities != null;

                if (!m_commandHooked && MyAPIGateway.Utilities != null)
                {
                    MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;
                    m_commandHooked = true;
                }

                m_stop.Deliver(gameOpen, Notify);

                if (m_patched && !m_stop.IsStopped && ++m_counter >= CoexistenceInterval)
                {
                    m_counter = 0;
                    StepAsideIfTaken();
                }
            }
            catch (Exception e)
            {
                // Une défaillance ici ne doit jamais faire tomber le jeu. Déjà
                // arrêté : le journal n'est pas répété à chaque image, mais la
                // faute reste comptée dans son rapport.
                if (!m_stop.Stop("faute sur le fil principal", Texts.StopFault, e) && s_log.Reporter != null)
                    s_log.Reporter.Record("faute sur le fil principal", e);
            }

            if (m_reporting != null)
                m_reporting.Update();
        }

        private static void Notify(string message)
        {
            var red = message.StartsWith(Texts.StopPrefix, StringComparison.Ordinal);
            MyAPIGateway.Utilities.ShowNotification(message, red ? 10000 : 3000, red ? "Red" : "White");
        }

        private void OnMessageEntered(string text, ref bool sendToOthers)
        {
            var action = CelCommand.Parse(text);
            if (action == CelAction.None)
                return;

            // La commande reste sur la machine du joueur.
            sendToOthers = false;

            var settings = Settings;
            if (action == CelAction.Unknown)
            {
                m_stop.Inform(Texts.CommandUnknown);
                return;
            }
            if (action == CelAction.Help)
            {
                m_stop.Inform(Texts.CommandHelp);
                return;
            }

            if (CelCommand.Apply(action, settings))
                Apply(settings);

            m_stop.Inform(Texts.Status(settings.Enabled, m_stop.Reason));
        }

        // Pris en compte aussitôt, sans relancer le jeu : l'image suivante est
        // dessinée avec les nouveaux réglages, qui sont enregistrés pour les
        // prochaines parties.
        public void Apply(Settings settings)
        {
            FinalColorsPass.CurrentSettings = settings;
            try
            {
                SettingsFile.Save(m_settingsDirectory, FinalColorsPass.CurrentSettings);
            }
            catch (Exception e)
            {
                LogError("réglages non enregistrés", e);
            }
        }

        // Appelée par Pulsar, le bouton de réglages du greffon.
        public void OpenConfigDialog()
        {
            MyGuiSandbox.AddScreen(new SettingsScreen(this));
        }

        public void Dispose()
        {
            try
            {
                if (m_commandHooked && MyAPIGateway.Utilities != null)
                    MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;
                m_commandHooked = false;

                // Seulement nos correctifs, jamais ceux d'un autre.
                if (m_harmony != null)
                    m_harmony.UnpatchAll(Id);
            }
            catch (Exception e)
            {
                LogError("arrêt incomplet", e);
            }

            // La sortie propre : les rapports attendent sur le disque la prochaine
            // connexion à nos serveurs, et aucun plantage ne sera rapporté.
            if (m_reporting != null)
                m_reporting.End();
            Instance = null;
        }
    }
}
