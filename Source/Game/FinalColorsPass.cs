using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using VRageRender;

namespace SirCelShading
{
    // Le préfixe, le postfixe et le finaliseur posés sur MyToneMapping.Run, la
    // passe des couleurs finales du jeu (méthode éprouvée sur #208).
    //
    // Activé : le préfixe met notre variante dans le champ statique de la
    // variante en cours, et lie la profondeur de la scène en t31 et l'albédo en
    // t27 ; le postfixe rend au champ son shader du jeu et les délie. Coupé, ou
    // arrêté : rien n'est touché, le jeu dessine avec ses propres shaders.
    //
    // Tout ce qui suit s'exécute sur le fil de rendu, et seulement là.
    internal static class FinalColorsPass
    {
        // Posés une fois par le greffon, avant que le correctif soit placé.
        public static RenderEngine Engine;
        public static SessionStop Stop;
        public static string ShaderPath;

        // Instantané des réglages, remplacé en bloc par le fil principal.
        private static volatile Settings s_settings = new Settings();

        public static Settings CurrentSettings
        {
            get { return s_settings; }
            set { s_settings = (value ?? new Settings()).Normalized(); }
        }

        // Variantes déjà créées par le jeu, par signature de réglages.
        private static readonly Dictionary<string, object[]> s_variants = new Dictionary<string, object[]>();

        // Ce que le préfixe a changé, pour que le postfixe le rende.
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
                    return; // GBuffer pas encore prêt : cette image reste celle du jeu

                // Nul avec le multi-échantillonnage : l'effet garde alors
                // l'éclairage du jeu, le reste s'applique quand même.
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
                    "faute sur le fil de rendu avant les couleurs finales",
                    Texts.StopFault, e);
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
                    "faute sur le fil de rendu après les couleurs finales",
                    Texts.StopFault, e);
            }
        }

        // La passe du jeu a levé une exception : le postfixe ne s'est pas
        // exécuté. Le jeu retrouve quand même ses shaders, et l'exception suit
        // son chemin.
        public static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && (s_replacedField != null || s_boundStage != null))
            {
                try { GiveBack(); }
                catch (Exception e)
                {
                    SirCelShadingPlugin.LogError("impossible de rendre ses shaders au jeu", e);
                }
                if (Stop != null)
                    Stop.Stop(
                        "la passe des couleurs finales a échoué avec notre variante",
                        Texts.StopFault, __exception);
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
                // Délier un emplacement jamais lié est sans danger.
                Engine.SetSrv.Invoke(stage, s_unbindDepth);
                Engine.SetSrv.Invoke(stage, s_unbindAlbedo);
            }
        }

        // Les trois variantes pour ces réglages, compilées par le jeu à la
        // première demande. Nul si l'une est refusée : l'effet est alors arrêté
        // pour la session, jamais servi à moitié.
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

                // D'abord le compilateur du jeu, qui refuse sans planter : nul, ou
                // une exception du compilateur HLSL.
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
                    ShaderVariants.Refuse(Stop, description, ShaderVariants.RefusedByCompiler, e.InnerException ?? e);
                    return null;
                }

                if (code == null || code.Length == 0)
                {
                    ShaderVariants.Refuse(Stop, description, ShaderVariants.RefusedWithoutDetail, null);
                    return null;
                }

                // Puis la création par le jeu, qui la trouve dans son cache.
                try
                {
                    ids[(int)variant] = Engine.Create.Invoke(null, new object[] { ShaderPath, macros });
                }
                catch (TargetInvocationException e)
                {
                    ShaderVariants.Refuse(Stop, description, ShaderVariants.RefusedAtCreation, e.InnerException ?? e);
                    return null;
                }
            }

            s_variants[signature] = ids;
            return ids;
        }

        // Vérifié avant que le correctif soit placé : un en-tête manquant ferait
        // échouer la compilation dans le préprocesseur du jeu, qui s'arrête alors
        // sur un point d'arrêt de débogage.
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
