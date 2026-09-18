using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Full-screen underwater post effect for Unity 6 / URP Render Graph.
/// </summary>
public class Underwater : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Use the UnderwaterEffects material, not the UnderwaterFog Shader Graph material.")]
        public Material material;

        [Tooltip("Run after transparent objects so the effect is applied to the whole scene.")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        public Color color = new Color(0.05f, 0.35f, 0.5f, 1f);

        [Min(0f)]
        public float FogDensity = 1f;

        [Range(0f, 1f)]
        public float alpha;

        [Range(0f, 2f)]
        public float refraction = 0.1f;

        public Texture normalmap;
        public Vector4 UV = new Vector4(1f, 1f, 0.2f, 0.1f);
    }

    public Settings settings = new Settings();

    private UnderwaterPass pass;

    private sealed class UnderwaterPass : ScriptableRenderPass
    {
        private const string PassName = "Underwater Fullscreen Effect";
        private Material material;

        public void Setup(Settings featureSettings)
        {
            material = featureSettings.material;
            material.SetColor("_color", featureSettings.color);
            material.SetFloat("_FogDensity", featureSettings.FogDensity);
            material.SetFloat("_alpha", featureSettings.alpha);
            material.SetFloat("_refraction", featureSettings.refraction);
            material.SetVector("_normalUV", featureSettings.UV);

            if (featureSettings.normalmap != null)
                material.SetTexture("_NormalMap", featureSettings.normalmap);

            // A full-screen blit cannot sample the backbuffer directly.
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning("[Underwater] Skipped because the camera color is the backbuffer. " +
                                 "Use an injection point before AfterRendering.");
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
                return;

            // Never read from and write to the same active camera texture.
            TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
            destinationDescriptor.name = "CameraColor-Underwater";
            destinationDescriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);

            var blitParameters = new RenderGraphUtils.BlitMaterialParameters(
                source, destination, material, 0);
            renderGraph.AddBlitPass(blitParameters, PassName);

            // Subsequent URP passes now consume the processed texture directly.
            resourceData.cameraColor = destination;
        }
    }

    public override void Create()
    {
        pass = new UnderwaterPass
        {
            renderPassEvent = settings.renderPassEvent
        };

        pass.ConfigureInput(ScriptableRenderPassInput.Depth);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.Preview ||
            renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        pass.renderPassEvent = settings.renderPassEvent;
        pass.Setup(settings);
        renderer.EnqueuePass(pass);
    }
}
