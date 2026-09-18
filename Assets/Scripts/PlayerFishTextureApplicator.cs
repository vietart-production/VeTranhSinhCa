using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Binds a generated player texture to private material instances before the fish is activated.
/// </summary>
public class PlayerFishTextureApplicator : MonoBehaviour
{
    private Texture2D ownedTexture;
    private readonly List<Material> ownedMaterials = new List<Material>();

    public static PlayerFishTextureApplicator Apply(
        GameObject fishInstance,
        Texture2D texture,
        float emissionIntensity = 0f,
        Color? emissionTint = null,
        float textureUvInset = 0f,
        Vector2? textureUvScale = null,
        Vector2? textureUvOffset = null,
        float alphaCutoff = 0.1f,
        Color? baseColorTint = null)
    {
        if (fishInstance == null || texture == null)
            return null;

        Renderer[] renderers = fishInstance.GetComponentsInChildren<Renderer>(true);
        var runtimeMaterials = new List<Material>();
        int boundMaterialCount = 0;

        foreach (Renderer targetRenderer in renderers)
        {
            // Player fish must participate in the camera depth texture. The source rig
            // materials are transparent (ZWrite Off), so the underwater full-screen pass
            // sees the background depth and occasionally washes the fish out completely.
            // Alpha-clipped opaque rendering keeps the transparent silhouette while writing
            // a stable depth value for every visible fish pixel.
            targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            targetRenderer.receiveShadows = false;

            Material[] sourceMaterials = targetRenderer.sharedMaterials;
            Material[] instanceMaterials = new Material[sourceMaterials.Length];

            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material sourceMaterial = sourceMaterials[i];
                if (sourceMaterial == null)
                    continue;

                Material instanceMaterial = new Material(sourceMaterial)
                {
                    name = sourceMaterial.name + "_PlayerInstance"
                };

                ConfigureAlphaClippedDepthMaterial(instanceMaterial, alphaCutoff);
                ConfigureBaseColor(instanceMaterial, baseColorTint ?? Color.white);

                bool textureWasBound = false;
                BindTexture(
                    instanceMaterial,
                    "_BaseMap",
                    texture,
                    textureUvInset,
                    textureUvScale ?? Vector2.one,
                    textureUvOffset ?? Vector2.zero,
                    ref textureWasBound);
                BindTexture(
                    instanceMaterial,
                    "_MainTex",
                    texture,
                    textureUvInset,
                    textureUvScale ?? Vector2.one,
                    textureUvOffset ?? Vector2.zero,
                    ref textureWasBound);

                ConfigureEmission(
                    instanceMaterial,
                    texture,
                    emissionTint ?? Color.white,
                    emissionIntensity);

                instanceMaterials[i] = instanceMaterial;
                runtimeMaterials.Add(instanceMaterial);

                if (textureWasBound)
                {
                    boundMaterialCount++;
                    Debug.Log($"[FishTexture] Bound {texture.name} to " +
                              $"{targetRenderer.name}/{instanceMaterial.shader.name}; " +
                              $"scale={instanceMaterial.mainTextureScale}, " +
                              $"offset={instanceMaterial.mainTextureOffset}, wrap={texture.wrapMode}");
                }
            }

            // Assign the private instances directly; do not rely on a property block.
            targetRenderer.sharedMaterials = instanceMaterials;
        }

        if (boundMaterialCount == 0)
        {
            foreach (Material material in runtimeMaterials)
                Object.Destroy(material);

            Debug.LogError("[FishTexture] Không tìm thấy material có _BaseMap hoặc _MainTex.");
            return null;
        }

        PlayerFishTextureApplicator applicator = fishInstance.AddComponent<PlayerFishTextureApplicator>();
        applicator.ownedTexture = texture;
        applicator.ownedMaterials.AddRange(runtimeMaterials);
        return applicator;
    }

    static void ConfigureAlphaClippedDepthMaterial(Material material, float alphaCutoff)
    {
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.renderQueue = (int)RenderQueue.AlphaTest;

        material.EnableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");

        SetFloatIfPresent(material, "_Surface", 0f);
        SetFloatIfPresent(material, "_AlphaClip", 1f);
        SetFloatIfPresent(material, "_Cutoff", Mathf.Clamp01(alphaCutoff));
        SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.One);
        SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.Zero);
        SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
        SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.Zero);
        SetFloatIfPresent(material, "_ZWrite", 1f);
        SetFloatIfPresent(material, "_ReceiveShadows", 0f);

        material.SetShaderPassEnabled("DepthOnly", true);
        material.SetShaderPassEnabled("ShadowCaster", false);
    }

    static void ConfigureBaseColor(Material material, Color tint)
    {
        tint.a = 1f;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", tint);
    }

    static void ConfigureEmission(
        Material material,
        Texture texture,
        Color tint,
        float intensity)
    {
        float safeIntensity = Mathf.Max(0f, intensity);
        bool supportsEmission = material.HasProperty("_EmissionColor");
        if (!supportsEmission)
            return;

        if (material.HasProperty("_EmissionMap"))
        {
            material.SetTexture("_EmissionMap", texture);

            // Emission phai dung cung bien doi UV voi Base Map, neu khong duong
            // noi UV se van hien lai khi bat emission.
            string sourceProperty = material.HasProperty("_BaseMap")
                ? "_BaseMap"
                : "_MainTex";
            if (material.HasProperty(sourceProperty))
            {
                material.SetTextureScale(
                    "_EmissionMap", material.GetTextureScale(sourceProperty));
                material.SetTextureOffset(
                    "_EmissionMap", material.GetTextureOffset(sourceProperty));
            }
        }

        // Dung chinh texture mau lam emission map de tang do sang ma khong
        // lam mat mau to cua nguoi choi.
        Color emissionColor = tint * safeIntensity;
        emissionColor.a = 1f;
        material.SetColor("_EmissionColor", emissionColor);

        if (safeIntensity > 0.0001f)
            material.EnableKeyword("_EMISSION");
        else
            material.DisableKeyword("_EMISSION");
    }

    static void BindTexture(
        Material material,
        string propertyName,
        Texture texture,
        float uvInset,
        Vector2 uvScale,
        Vector2 uvOffset,
        ref bool textureWasBound)
    {
        if (!material.HasProperty(propertyName))
            return;

        Vector2 originalScale = material.GetTextureScale(propertyName);
        Vector2 originalOffset = material.GetTextureOffset(propertyName);
        float safeInset = Mathf.Clamp(uvInset, 0f, 0.2f);
        float insetScale = 1f - safeInset * 2f;

        material.SetTexture(propertyName, texture);
        material.SetTextureScale(
            propertyName,
            Vector2.Scale(originalScale, uvScale) * insetScale);
        material.SetTextureOffset(
            propertyName,
            (Vector2.Scale(originalOffset, uvScale) + uvOffset) * insetScale +
            Vector2.one * safeInset);
        textureWasBound = true;
    }

    static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    void OnDestroy()
    {
        foreach (Material material in ownedMaterials)
        {
            if (material != null)
                Destroy(material);
        }

        if (ownedTexture != null)
            Destroy(ownedTexture);
    }
}
