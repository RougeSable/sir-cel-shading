using System;
using System.Linq;
using System.Reflection;
using SharpDX.Direct3D;
using VRageRender;

namespace SirCelShading
{
    // Tout ce que le greffon utilise du moteur de rendu du client
    // (VRage.Render11), résolu par réflexion une fois pour toutes. Un seul nom
    // introuvable, ou une forme inattendue, et Resoudre renvoie null avec le
    // nom en cause : l'effet s'arrête pour la session sans rien toucher.
    //
    // Noms relevés sur jeu-installe/Bin64/VRage.Render11.dll décompilé.
    internal sealed class MoteurDeRendu
    {
        private const BindingFlags Statique = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // VRageRender.MyToneMapping.Run(ISrvBindable, ISrvBindable, ISrvBindable, bool, string, bool)
        public MethodInfo Run;
        public int IndexActiverTonemapping;
        public int IndexLuminanceAlpha;

        // m_cs, m_csAlphaLuminance, m_csSkip : des MyComputeShaders.Id.
        public FieldInfo[] Champs;
        public Type TypeId;

        // MyShaderCompiler.Compile(string, ShaderMacro[], MyShaderProfile, string, bool) : byte[]
        // Renvoie null quand le shader est refusé, sans planter.
        public MethodInfo Compiler;

        // MyComputeShaders.Create(string, ShaderMacro[]) : MyComputeShaders.Id
        public MethodInfo Creer;

        // MyRender11.RC.ComputeShader.SetSrv(int, ISrvBindable)
        public PropertyInfo ContexteDeRendu;
        public PropertyInfo EtageCalcul;
        public MethodInfo LierRessource;

        // MyGBuffer.Main.ResolvedDepthStencil.SrvDepth
        public FieldInfo GBufferPrincipal;
        public PropertyInfo ProfondeurResolue;
        public PropertyInfo VueProfondeur;

        public static MoteurDeRendu Resoudre(out string manquant)
        {
            manquant = null;
            var r = new MoteurDeRendu();
            var rendu = typeof(MyShaderCompiler).Assembly;

            var toneMapping = rendu.GetType("VRageRender.MyToneMapping");
            if (toneMapping == null) { manquant = "VRageRender.MyToneMapping"; return null; }

            r.Run = toneMapping.GetMethod("Run", Statique);
            if (r.Run == null) { manquant = "MyToneMapping.Run"; return null; }

            var parametres = r.Run.GetParameters();
            if (parametres.Length != 6
                || parametres[3].ParameterType != typeof(bool)
                || parametres[5].ParameterType != typeof(bool))
            {
                manquant = "MyToneMapping.Run(src, avgLum, bloom, bool enableTonemapping, string, bool needsAlphaLuminance)";
                return null;
            }
            r.IndexActiverTonemapping = 3;
            r.IndexLuminanceAlpha = 5;

            r.Champs = new FieldInfo[VariantesDuShader.NombreDeVariantes];
            for (var i = 0; i < r.Champs.Length; i++)
            {
                var nom = VariantesDuShader.ChampsDuJeu[i];
                r.Champs[i] = toneMapping.GetField(nom, Statique);
                if (r.Champs[i] == null || r.Champs[i].IsInitOnly) { manquant = "MyToneMapping." + nom; return null; }
            }

            var computeShaders = rendu.GetType("VRageRender.MyComputeShaders");
            r.TypeId = rendu.GetType("VRageRender.MyComputeShaders+Id");
            if (computeShaders == null || r.TypeId == null) { manquant = "VRageRender.MyComputeShaders.Id"; return null; }
            if (r.Champs.Any(c => c.FieldType != r.TypeId)) { manquant = "MyToneMapping.m_cs de type MyComputeShaders.Id"; return null; }

            r.Creer = computeShaders.GetMethod("Create", Statique, null,
                new[] { typeof(string), typeof(ShaderMacro[]) }, null);
            if (r.Creer == null || r.Creer.ReturnType != r.TypeId) { manquant = "MyComputeShaders.Create(string, ShaderMacro[])"; return null; }

            r.Compiler = typeof(MyShaderCompiler).GetMethod("Compile", Statique, null,
                new[] { typeof(string), typeof(ShaderMacro[]), typeof(MyShaderProfile), typeof(string), typeof(bool) }, null);
            if (r.Compiler == null || r.Compiler.ReturnType != typeof(byte[])) { manquant = "MyShaderCompiler.Compile(string, ShaderMacro[], MyShaderProfile, string, bool)"; return null; }

            var render11 = rendu.GetType("VRageRender.MyRender11");
            r.ContexteDeRendu = render11 == null ? null : render11.GetProperty("RC", Statique);
            if (r.ContexteDeRendu == null) { manquant = "MyRender11.RC"; return null; }

            r.EtageCalcul = r.ContexteDeRendu.PropertyType.GetProperty("ComputeShader", Instance);
            if (r.EtageCalcul == null) { manquant = "MyRenderContext.ComputeShader"; return null; }

            var ressource = rendu.GetType("VRage.Render11.Resources.ISrvBindable");
            if (ressource == null) { manquant = "VRage.Render11.Resources.ISrvBindable"; return null; }

            r.LierRessource = r.EtageCalcul.PropertyType.GetMethod("SetSrv", Instance, null,
                new[] { typeof(int), ressource }, null);
            if (r.LierRessource == null) { manquant = "MyCommonStage.SetSrv(int, ISrvBindable)"; return null; }

            var gbuffer = rendu.GetType("VRage.Render11.Resources.MyGBuffer");
            r.GBufferPrincipal = gbuffer == null ? null : gbuffer.GetField("Main", Statique);
            if (r.GBufferPrincipal == null) { manquant = "MyGBuffer.Main"; return null; }

            r.ProfondeurResolue = gbuffer.GetProperty("ResolvedDepthStencil", Instance);
            if (r.ProfondeurResolue == null) { manquant = "MyGBuffer.ResolvedDepthStencil"; return null; }

            // La propriété est déclarée sur l'interface IDepthStencil.
            r.VueProfondeur = r.ProfondeurResolue.PropertyType.GetProperty("SrvDepth", Instance);
            if (r.VueProfondeur == null || !ressource.IsAssignableFrom(r.VueProfondeur.PropertyType))
            {
                manquant = "IDepthStencil.SrvDepth";
                return null;
            }

            return r;
        }

        public static ShaderMacro[] VersSharpDX(System.Collections.Generic.IList<DefinitionDeMacro> macros)
        {
            var resultat = new ShaderMacro[macros.Count];
            for (var i = 0; i < macros.Count; i++)
                resultat[i] = new ShaderMacro(macros[i].Nom, macros[i].Valeur);
            return resultat;
        }

        // La profondeur de la scène, ou null si le GBuffer n'est pas prêt.
        public object Profondeur()
        {
            var principal = GBufferPrincipal.GetValue(null);
            if (principal == null)
                return null;
            var profondeur = ProfondeurResolue.GetValue(principal, null);
            if (profondeur == null)
                return null;
            return VueProfondeur.GetValue(profondeur, null);
        }

        public object EtageDeCalcul()
        {
            var contexte = ContexteDeRendu.GetValue(null, null);
            return contexte == null ? null : EtageCalcul.GetValue(contexte, null);
        }
    }
}
