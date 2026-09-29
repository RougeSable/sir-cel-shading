using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SharpDX.Direct3D;
using VRageRender;

namespace SirCelShading
{
    // Everything the plugin uses from the client's render engine
    // (VRage.Render11), resolved by reflection once and for all. A single
    // missing name, or an unexpected shape, and Resolve returns null with the
    // name at fault: the effect stops for the session without touching
    // anything.
    //
    // Names taken from the decompiled jeu-installe/Bin64/VRage.Render11.dll.
    internal sealed class RenderEngine
    {
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // VRageRender.MyToneMapping.Run(ISrvBindable, ISrvBindable, ISrvBindable, bool, string, bool)
        public MethodInfo Run;
        public int EnableTonemappingIndex;
        public int AlphaLuminanceIndex;

        // m_cs, m_csAlphaLuminance, m_csSkip: MyComputeShaders.Id values.
        public FieldInfo[] Fields;
        public Type IdType;

        // MyShaderCompiler.Compile(string, ShaderMacro[], MyShaderProfile, string, bool): byte[]
        // Returns null when the shader is refused, without crashing.
        public MethodInfo Compile;

        // MyComputeShaders.Create(string, ShaderMacro[]): MyComputeShaders.Id
        public MethodInfo Create;

        // MyRender11.RC.ComputeShader.SetSrv(int, ISrvBindable)
        public PropertyInfo RenderContext;
        public PropertyInfo ComputeStage;
        public MethodInfo SetSrv;

        // MyGBuffer.Main.ResolvedDepthStencil.SrvDepth
        public FieldInfo MainGBuffer;
        public PropertyInfo ResolvedDepth;
        public PropertyInfo DepthView;

        // MyGBuffer.Main.GBuffer0 (albedo) and MyGBuffer.SamplesCount: with
        // multisampling, GBuffer0 is not a plain texture and is not bound.
        public PropertyInfo Albedo;
        public PropertyInfo SamplesCount;

        public static RenderEngine Resolve(out string missing)
        {
            missing = null;
            var r = new RenderEngine();
            var render = typeof(MyShaderCompiler).Assembly;

            var toneMapping = render.GetType("VRageRender.MyToneMapping");
            if (toneMapping == null) { missing = "VRageRender.MyToneMapping"; return null; }

            r.Run = toneMapping.GetMethod("Run", Static);
            if (r.Run == null) { missing = "MyToneMapping.Run"; return null; }

            var parameters = r.Run.GetParameters();
            if (parameters.Length != 6
                || parameters[3].ParameterType != typeof(bool)
                || parameters[5].ParameterType != typeof(bool))
            {
                missing = "MyToneMapping.Run(src, avgLum, bloom, bool enableTonemapping, string, bool needsAlphaLuminance)";
                return null;
            }
            r.EnableTonemappingIndex = 3;
            r.AlphaLuminanceIndex = 5;

            r.Fields = new FieldInfo[ShaderVariants.VariantCount];
            for (var i = 0; i < r.Fields.Length; i++)
            {
                var name = ShaderVariants.GameFields[i];
                r.Fields[i] = toneMapping.GetField(name, Static);
                if (r.Fields[i] == null || r.Fields[i].IsInitOnly) { missing = "MyToneMapping." + name; return null; }
            }

            var computeShaders = render.GetType("VRageRender.MyComputeShaders");
            r.IdType = render.GetType("VRageRender.MyComputeShaders+Id");
            if (computeShaders == null || r.IdType == null) { missing = "VRageRender.MyComputeShaders.Id"; return null; }
            if (r.Fields.Any(f => f.FieldType != r.IdType)) { missing = "MyToneMapping.m_cs of type MyComputeShaders.Id"; return null; }

            r.Create = computeShaders.GetMethod("Create", Static, null,
                new[] { typeof(string), typeof(ShaderMacro[]) }, null);
            if (r.Create == null || r.Create.ReturnType != r.IdType) { missing = "MyComputeShaders.Create(string, ShaderMacro[])"; return null; }

            r.Compile = typeof(MyShaderCompiler).GetMethod("Compile", Static, null,
                new[] { typeof(string), typeof(ShaderMacro[]), typeof(MyShaderProfile), typeof(string), typeof(bool) }, null);
            if (r.Compile == null || r.Compile.ReturnType != typeof(byte[])) { missing = "MyShaderCompiler.Compile(string, ShaderMacro[], MyShaderProfile, string, bool)"; return null; }

            var render11 = render.GetType("VRageRender.MyRender11");
            r.RenderContext = render11 == null ? null : render11.GetProperty("RC", Static);
            if (r.RenderContext == null) { missing = "MyRender11.RC"; return null; }

            r.ComputeStage = r.RenderContext.PropertyType.GetProperty("ComputeShader", Instance);
            if (r.ComputeStage == null) { missing = "MyRenderContext.ComputeShader"; return null; }

            var resource = render.GetType("VRage.Render11.Resources.ISrvBindable");
            if (resource == null) { missing = "VRage.Render11.Resources.ISrvBindable"; return null; }

            r.SetSrv = r.ComputeStage.PropertyType.GetMethod("SetSrv", Instance, null,
                new[] { typeof(int), resource }, null);
            if (r.SetSrv == null) { missing = "MyCommonStage.SetSrv(int, ISrvBindable)"; return null; }

            var gbuffer = render.GetType("VRage.Render11.Resources.MyGBuffer");
            r.MainGBuffer = gbuffer == null ? null : gbuffer.GetField("Main", Static);
            if (r.MainGBuffer == null) { missing = "MyGBuffer.Main"; return null; }

            r.ResolvedDepth = gbuffer.GetProperty("ResolvedDepthStencil", Instance);
            if (r.ResolvedDepth == null) { missing = "MyGBuffer.ResolvedDepthStencil"; return null; }

            // The property is declared on the IDepthStencil interface.
            r.DepthView = r.ResolvedDepth.PropertyType.GetProperty("SrvDepth", Instance);
            if (r.DepthView == null || !resource.IsAssignableFrom(r.DepthView.PropertyType))
            {
                missing = "IDepthStencil.SrvDepth";
                return null;
            }

            // IRtvTexture, which extends ISrvBindable.
            r.Albedo = gbuffer.GetProperty("GBuffer0", Instance);
            if (r.Albedo == null || !resource.IsAssignableFrom(r.Albedo.PropertyType))
            {
                missing = "MyGBuffer.GBuffer0";
                return null;
            }

            r.SamplesCount = gbuffer.GetProperty("SamplesCount", Instance);
            if (r.SamplesCount == null || r.SamplesCount.PropertyType != typeof(int))
            {
                missing = "MyGBuffer.SamplesCount";
                return null;
            }

            return r;
        }

        public static ShaderMacro[] ToSharpDX(IList<MacroDefinition> macros)
        {
            var result = new ShaderMacro[macros.Count];
            for (var i = 0; i < macros.Count; i++)
                result[i] = new ShaderMacro(macros[i].Name, macros[i].Value);
            return result;
        }

        // The scene depth, or null while the GBuffer is not ready.
        public object Depth()
        {
            var main = MainGBuffer.GetValue(null);
            if (main == null)
                return null;
            var depth = ResolvedDepth.GetValue(main, null);
            if (depth == null)
                return null;
            return DepthView.GetValue(depth, null);
        }

        // The scene albedo, or null while the GBuffer is not ready or when it is
        // multisampled: the shader then keeps the game's lighting.
        public object AlbedoTexture()
        {
            var main = MainGBuffer.GetValue(null);
            if (main == null)
                return null;
            if ((int)SamplesCount.GetValue(main, null) > 1)
                return null;
            return Albedo.GetValue(main, null);
        }

        public object ComputeShaderStage()
        {
            var context = RenderContext.GetValue(null, null);
            return context == null ? null : ComputeStage.GetValue(context, null);
        }
    }
}
