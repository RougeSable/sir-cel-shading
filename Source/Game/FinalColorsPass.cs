using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using VRageRender;

namespace SirCelShading
{
    // The prefix, postfix and finalizer placed on MyToneMapping.Run, the
    // game's final colors pass (method proven on #208).
    //
    // Enabled: the prefix puts our variant in the static field of the current
    // variant, and binds the scene depth in t31 and the albedo in t27; the
    // postfix gives the field back its game shader and unbinds them. Disabled, or
    // stopped: nothing is touched, the game draws with its own shaders.
    //
    // Everything below runs on the render thread, and only there.
    internal static class FinalColorsPass
    {
        // Set once by the plugin, before the patch is placed.
        public static RenderEngine Engine;
        public static SessionStop Stop;
        public static string ShaderPath;

        // Snapshot of the settings, replaced as a whole by the main thread.
        private static volatile Settings s_settings = new Settings();

        public static Settings CurrentSettings
        {
            get { return s_settings; }
            set { s_settings = (value ?? new Settings()).Normalized(); }
        }

        // Variants already created by the game, by settings signature.
        private static readonly Dictionary<string, object[]> s_variants = new Dictionary<string, object[]>();

        // What the prefix changed, for the postfix to give back.
        private static FieldInfo s_replacedField;
        private static object s_gameShader;
        private static object s_boundStage;

        private static readonly object[] s_unbindDepth = { ShaderSource.DepthSlot, null };
        private static readonly object[] s_unbindAlbedo = { ShaderSource.AlbedoSlot, null };
        private static readonly object[] s_bind = { 0, null };

        public static void Prefix(object[] __args)
        {
            try
            {
                if (Stop == null || Stop.IsStopped || Engine == null)
                    return;

                var settings = s_settings;
                if (!settings.Enabled)
                    return;

                var ids = Variants(settings);
                if (ids == null)
                    return;

                var stage = Engine.ComputeShaderStage();
                var depth = Engine.Depth();
                if (stage == null || depth == null)
                    return; // GBuffer not ready yet: this frame stays the game's

                // Null with multisampling: the effect then keeps the game's
                // lighting, the rest of it still applies.
                var albedo = Engine.AlbedoTexture();

                var variant = ShaderVariants.Choose(
                    (bool)__args[Engine.EnableTonemappingIndex],
                    (bool)__args[Engine.AlphaLuminanceIndex]);
                var field = Engine.Fields[(int)variant];

                s_gameShader = field.GetValue(null);
                field.SetValue(null, ids[(int)variant]);
                s_replacedField = field;

                s_boundStage = stage;
                Bind(stage, ShaderSource.DepthSlot, depth);
                if (albedo != null)
                    Bind(stage, ShaderSource.AlbedoSlot, albedo);
            }
            catch (Exception e)
            {
                GiveBack();
                Stop.Stop(
                    "fault on the render thread before the final colors: " + e,
                    Texts.StopFault);
            }
        }

        private static void Bind(object stage, int slot, object resource)
        {
            s_bind[0] = slot;
            s_bind[1] = resource;
            try
            {
                Engine.SetSrv.Invoke(stage, s_bind);
            }
            finally
            {
                s_bind[1] = null;
            }
        }

        public static void Postfix()
        {
            try
            {
                GiveBack();
            }
            catch (Exception e)
            {
                Stop.Stop(
                    "fault on the render thread after the final colors: " + e,
                    Texts.StopFault);
            }
        }

        // The game's pass threw: the postfix did not run. The game still gets
        // its shaders back, and the exception goes on its way.
        public static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && (s_replacedField != null || s_boundStage != null))
            {
                try { GiveBack(); }
                catch (Exception) { }
                if (Stop != null)
                    Stop.Stop(
                        "the final colors pass failed with our variant: " + __exception,
                        Texts.StopFault);
            }
            return __exception;
        }

        private static void GiveBack()
        {
            if (s_replacedField != null)
            {
                var field = s_replacedField;
                s_replacedField = null;
                field.SetValue(null, s_gameShader);
                s_gameShader = null;
            }

            if (s_boundStage != null)
            {
                var stage = s_boundStage;
                s_boundStage = null;
                // Unbinding a slot that was never bound is harmless.
                Engine.SetSrv.Invoke(stage, s_unbindDepth);
                Engine.SetSrv.Invoke(stage, s_unbindAlbedo);
            }
        }

        // The three variants for these settings, compiled by the game on first
        // request. Null if one is refused: the effect is then stopped for the
        // session, never served by halves.
        private static object[] Variants(Settings settings)
        {
            var signature = ShaderVariants.Signature(settings);
            object[] ids;
            if (s_variants.TryGetValue(signature, out ids))
                return ids;

            ids = new object[ShaderVariants.VariantCount];
            foreach (ShaderVariant variant in Enum.GetValues(typeof(ShaderVariant)))
            {
                var definitions = ShaderVariants.Macros(variant, settings);
                var macros = RenderEngine.ToSharpDX(definitions);
                var description = variant + " (" + string.Join(" ", definitions) + ")";

                // The game's compiler first, which refuses without crashing:
                // null, or an exception from the HLSL compiler.
                byte[] code;
                try
                {
                    code = (byte[])Engine.Compile.Invoke(null, new object[]
                    {
                        ShaderPath, macros, MyShaderProfile.cs_5_0, ShaderPath, false,
                    });
                }
                catch (TargetInvocationException e)
                {
                    Stop.Stop(
                        "variant " + description + " refused by the game's compiler: " + e.InnerException,
                        Texts.StopVariantRefused);
                    return null;
                }

                if (code == null || code.Length == 0)
                {
                    Stop.Stop(
                        "variant " + description + " refused by the game's compiler (details in the render log)",
                        Texts.StopVariantRefused);
                    return null;
                }

                // Then the creation by the game, which finds it in its cache.
                try
                {
                    ids[(int)variant] = Engine.Create.Invoke(null, new object[] { ShaderPath, macros });
                }
                catch (TargetInvocationException e)
                {
                    Stop.Stop(
                        "variant " + description + " refused at creation: " + e.InnerException,
                        Texts.StopVariantRefused);
                    return null;
                }
            }

            s_variants[signature] = ids;
            return ids;
        }

        // Checked before the patch is placed: a missing header would make the
        // compilation fail in the game's preprocessor, which then stops on a
        // debug break.
        public static string MissingHeader(string shaderDirectory)
        {
            foreach (var header in ShaderSource.GameHeaders)
            {
                if (!File.Exists(Path.Combine(shaderDirectory, header)))
                    return header;
            }
            return null;
        }
    }
}
