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
    // Sir Cel Shading, a client-side plugin loaded by Pulsar. It only does cel
    // shading, in the Animated film style, on the player's machine: nothing goes through
    // the server, a player without the plugin sees the game's rendering.
    public class SirCelShadingPlugin : IPlugin
    {
        public const string Id = "sir-cel-shading";

        // How many updates between two checks that no other plugin hooked the
        // final colors after us (60 per second).
        private const int CoexistenceInterval = 600;

        private Harmony m_harmony;
        private RenderEngine m_engine;
        private SessionStop m_stop;
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
            MyLog.Default.WriteLine("[" + Id + "] " + text);
        }

        public void Init(object gameInstance)
        {
            Instance = this;
            m_stop = new SessionStop(Log);
            FinalColorsPass.Stop = m_stop;

            // The player's settings folder: %AppData%\SpaceEngineers\Storage\sir-cel-shading
            m_settingsDirectory = Path.Combine(MyFileSystem.UserDataPath, "Storage", Id);

            string problem;
            FinalColorsPass.CurrentSettings = SettingsFile.Load(m_settingsDirectory, out problem);
            if (problem != null)
                Log(problem + "; using the defaults");

            var settings = FinalColorsPass.CurrentSettings;
            Log("loaded, Animated film, " + (settings.Enabled ? "on" : "off"));

            try
            {
                Prepare(m_settingsDirectory);
            }
            catch (Exception e)
            {
                m_stop.Stop("preparation failed: " + e, Texts.StopPatch);
            }
        }

        private void Prepare(string directory)
        {
            string missing;
            m_engine = RenderEngine.Resolve(out missing);
            if (m_engine == null)
            {
                m_stop.Stop("unexpected render engine, not found: " + missing, Texts.StopUnexpectedEngine);
                return;
            }
            FinalColorsPass.Engine = m_engine;

            var header = FinalColorsPass.MissingHeader(MyShaderCompiler.ShadersPath);
            if (header != null)
            {
                m_stop.Stop("game header not found: " + header + " in " + MyShaderCompiler.ShadersPath,
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
                m_stop.Stop("could not write " + shaderPath + ": " + e.Message, Texts.StopShaderFile);
                return;
            }
            FinalColorsPass.ShaderPath = shaderPath;

            // Two plugins never fight over the same step of the game.
            if (StepAsideIfTaken())
                return;

            m_harmony = new Harmony(Id);
            var pass = typeof(FinalColorsPass);
            m_harmony.Patch(m_engine.Run,
                prefix: new HarmonyMethod(pass.GetMethod("Prefix")),
                postfix: new HarmonyMethod(pass.GetMethod("Postfix")),
                finalizer: new HarmonyMethod(pass.GetMethod("Finalizer")));
            m_patched = true;
            Log("hooked on MyToneMapping.Run, depth in t" + ShaderSource.DepthSlot
                + ", albedo in t" + ShaderSource.AlbedoSlot + ", shader written to " + shaderPath);
        }

        private static void WriteShader(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var text = ShaderSource.Text;
            if (File.Exists(path) && File.ReadAllText(path) == text)
                return;
            File.WriteAllText(path, text);
        }

        // True if another plugin is hooked on the final colors: the effect then
        // stops for the session, and our patch, if placed,
        // stays inert (removing it while the render thread runs it could leave
        // a game field replaced).
        private bool StepAsideIfTaken()
        {
            var info = Harmony.GetPatchInfo(m_engine.Run);
            var others = Coexistence.OtherOwners(info == null ? null : info.Owners, Id);
            if (others.Count == 0)
                return false;

            var names = string.Join(", ", others.ToArray());
            m_stop.Stop("MyToneMapping.Run is already patched by " + names + ": stepping aside",
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
                // A failure here must never bring the game down.
                m_stop.Stop("fault on the main thread: " + e, Texts.StopFault);
            }
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

            // The command stays on the player's machine.
            sendToOthers = false;

            var settings = Settings;
            if (action == CelAction.Unknown)
            {
                m_stop.Inform(Texts.CommandHelp);
                return;
            }

            if (CelCommand.Apply(action, settings))
                Apply(settings);

            m_stop.Inform(Texts.Status(settings.Enabled, m_stop.Reason));
        }

        // Taken into account at once, without restarting the game: the next
        // frame is drawn with the new settings, which are saved for the next
        // games.
        public void Apply(Settings settings)
        {
            FinalColorsPass.CurrentSettings = settings;
            try
            {
                SettingsFile.Save(m_settingsDirectory, FinalColorsPass.CurrentSettings);
            }
            catch (Exception e)
            {
                Log("settings not saved: " + e.Message);
            }
        }

        // Called by Pulsar, the plugin's settings button.
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

                // Only our patches, never anyone else's.
                if (m_harmony != null)
                    m_harmony.UnpatchAll(Id);
            }
            catch (Exception e)
            {
                Log("incomplete shutdown: " + e.Message);
            }
            Instance = null;
        }
    }
}
