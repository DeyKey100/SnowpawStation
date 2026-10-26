using UnityEngine;

[DisallowMultipleComponent]
public sealed class PSXVertexSnap : MonoBehaviour
{
    private const string ShaderName = "Snowpaw/PSX Lit Vertex Snap";
    private const string OriginalPsxShaderName = "PSXGasStationKitFree/PSX Unlit";
    private const string MainTextureProperty = "_MainTex";
    private const string BaseMapProperty = "_BaseMap";
    private const string ColorProperty = "_Color";
    private const string BaseColorProperty = "_BaseColor";
    private const string BumpMapProperty = "_BumpMap";
    private const string BumpScaleProperty = "_BumpScale";
    private const string MetallicProperty = "_Metallic";
    private const string MetallicGlossMapProperty = "_MetallicGlossMap";
    private const string SmoothnessProperty = "_Smoothness";
    private const string OcclusionMapProperty = "_OcclusionMap";
    private const string OcclusionStrengthProperty = "_OcclusionStrength";
    private const string EmissionMapProperty = "_EmissionMap";
    private const string EmissionColorProperty = "_EmissionColor";
    private const string SnapRowsProperty = "_SnapRows";
    private const string CutoffProperty = "_Cutoff";
    private const string AlphaClipProperty = "_AlphaClip";
    private const string SurfaceProperty = "_Surface";
    private const string CullProperty = "_Cull";
    private const string ZWriteProperty = "_ZWrite";
    private const string SourceBlendProperty = "_SrcBlend";
    private const string DestinationBlendProperty = "_DstBlend";

    [SerializeField, Min(0f)] private float snapRows = 400f;
    [SerializeField, Range(0f, 1f)] private float alphaCutoff;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool convertNonPsxMaterials = true;

    private void Awake()
    {
        if (applyOnStart)
        {
            ApplyToRenderers();
        }
    }

    [ContextMenu("Apply PSX Vertex Snap")]
    public void ApplyToRenderers()
    {
        Shader psxShader = Shader.Find(ShaderName);
        if (psxShader == null)
        {
            Debug.LogError($"Не найден shader '{ShaderName}'.", this);
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null ||
                    materials[i].shader == null ||
                    materials[i].shader.name == OriginalPsxShaderName ||
                    !convertNonPsxMaterials)
                {
                    continue;
                }

                materials[i] = CreatePSXMaterial(materials[i], psxShader);
            }

            renderer.materials = materials;
        }
    }

    private Material CreatePSXMaterial(Material source, Shader psxShader)
    {
        if (source == null)
        {
            return CreatePSXMaterial(null, psxShader, "PSXVertexSnap");
        }

        Material result = new Material(source)
        {
            name = $"{source.name}_PSXVertexSnap",
            shader = psxShader
        };

        CopyTexture(source, result, BaseMapProperty, BaseMapProperty, MainTextureProperty);
        CopyTexture(source, result, BumpMapProperty, BumpMapProperty, BumpMapProperty);
        CopyTexture(
            source,
            result,
            MetallicGlossMapProperty,
            MetallicGlossMapProperty,
            MetallicGlossMapProperty);
        result.SetColor(
            BaseColorProperty,
            GetColor(source, BaseColorProperty, GetColor(source, ColorProperty, Color.white)));
        CopyFloat(source, result, BumpScaleProperty, BumpScaleProperty, 1f);
        CopyFloat(source, result, MetallicProperty, MetallicProperty, 0f);
        CopyFloat(source, result, SmoothnessProperty, SmoothnessProperty, 0.5f);
        CopyTexture(source, result, OcclusionMapProperty, OcclusionMapProperty, OcclusionMapProperty);
        CopyFloat(source, result, OcclusionStrengthProperty, OcclusionStrengthProperty, 1f);
        CopyTexture(source, result, EmissionMapProperty, EmissionMapProperty, EmissionMapProperty);
        CopyColor(source, result, EmissionColorProperty, EmissionColorProperty, Color.black);
        SetDefaultTexture(result, BaseMapProperty, Texture2D.whiteTexture);
        SetDefaultTexture(result, BumpMapProperty, Texture2D.normalTexture);
        SetDefaultTexture(result, MetallicGlossMapProperty, Texture2D.whiteTexture);
        SetDefaultTexture(result, OcclusionMapProperty, Texture2D.whiteTexture);
        SetDefaultTexture(result, EmissionMapProperty, Texture2D.blackTexture);
        CopyFloat(source, result, AlphaClipProperty, "_AlphaClip", 0f);
        CopyFloat(source, result, SurfaceProperty, SurfaceProperty, 0f);
        CopyFloat(source, result, CullProperty, CullProperty, 2f);
        CopyFloat(source, result, ZWriteProperty, ZWriteProperty, 1f);
        CopyFloat(source, result, SourceBlendProperty, SourceBlendProperty, 1f);
        CopyFloat(source, result, DestinationBlendProperty, DestinationBlendProperty, 0f);
        result.renderQueue = source.renderQueue;
        result.SetOverrideTag("RenderType", source.GetTag("RenderType", true, "Opaque"));
        result.doubleSidedGI = source.doubleSidedGI;
        result.SetFloat(SnapRowsProperty, snapRows);
        result.SetFloat(CutoffProperty, alphaCutoff);
        return result;
    }

    private Material CreatePSXMaterial(Material source, Shader psxShader, string materialName)
    {
        Material result = new Material(psxShader)
        {
            name = materialName
        };

        if (source != null)
        {
            CopyTexture(
                source,
                result,
                BaseMapProperty,
                BaseMapProperty,
                MainTextureProperty);
            CopyTexture(source, result, BumpMapProperty, BumpMapProperty, BumpMapProperty);
            CopyTexture(
                source,
                result,
                MetallicGlossMapProperty,
                MetallicGlossMapProperty,
                MetallicGlossMapProperty);
            result.SetColor(
                BaseColorProperty,
                GetColor(source, BaseColorProperty, GetColor(source, ColorProperty, Color.white)));
            CopyFloat(source, result, BumpScaleProperty, BumpScaleProperty, 1f);
            CopyFloat(source, result, MetallicProperty, MetallicProperty, 0f);
            CopyFloat(source, result, SmoothnessProperty, SmoothnessProperty, 0.5f);
            CopyTexture(source, result, OcclusionMapProperty, OcclusionMapProperty, OcclusionMapProperty);
            CopyFloat(source, result, OcclusionStrengthProperty, OcclusionStrengthProperty, 1f);
            CopyTexture(source, result, EmissionMapProperty, EmissionMapProperty, EmissionMapProperty);
            CopyColor(source, result, EmissionColorProperty, EmissionColorProperty, Color.black);
            SetDefaultTexture(result, BaseMapProperty, Texture2D.whiteTexture);
            SetDefaultTexture(result, BumpMapProperty, Texture2D.normalTexture);
            SetDefaultTexture(result, MetallicGlossMapProperty, Texture2D.whiteTexture);
            SetDefaultTexture(result, OcclusionMapProperty, Texture2D.whiteTexture);
            SetDefaultTexture(result, EmissionMapProperty, Texture2D.blackTexture);
            CopyFloat(source, result, AlphaClipProperty, "_AlphaClip", 0f);
            CopyFloat(source, result, SurfaceProperty, SurfaceProperty, 0f);
            CopyFloat(source, result, CullProperty, CullProperty, 2f);
            CopyFloat(source, result, ZWriteProperty, ZWriteProperty, 1f);
            CopyFloat(source, result, SourceBlendProperty, SourceBlendProperty, 1f);
            CopyFloat(source, result, DestinationBlendProperty, DestinationBlendProperty, 0f);
            result.renderQueue = source.renderQueue;
            result.SetOverrideTag("RenderType", source.GetTag("RenderType", true, "Opaque"));
            result.doubleSidedGI = source.doubleSidedGI;
        }

        result.SetFloat(SnapRowsProperty, snapRows);
        result.SetFloat(CutoffProperty, alphaCutoff);
        return result;
    }

    private static Texture GetTexture(Material material, string propertyName)
    {
        return material != null && material.HasProperty(propertyName)
            ? material.GetTexture(propertyName)
            : null;
    }

    private static Color GetColor(Material material, string propertyName, Color fallback)
    {
        return material != null && material.HasProperty(propertyName)
            ? material.GetColor(propertyName)
            : fallback;
    }

    private static Vector2 GetTextureScale(
        Material material,
        string preferredProperty,
        string fallbackProperty)
    {
        if (material != null && material.HasProperty(preferredProperty))
        {
            return material.GetTextureScale(preferredProperty);
        }

        return material != null && material.HasProperty(fallbackProperty)
            ? material.GetTextureScale(fallbackProperty)
            : Vector2.one;
    }

    private static Vector2 GetTextureOffset(
        Material material,
        string preferredProperty,
        string fallbackProperty)
    {
        if (material != null && material.HasProperty(preferredProperty))
        {
            return material.GetTextureOffset(preferredProperty);
        }

        return material != null && material.HasProperty(fallbackProperty)
            ? material.GetTextureOffset(fallbackProperty)
            : Vector2.zero;
    }

    private static void CopyTexture(
        Material source,
        Material destination,
        string destinationProperty,
        string preferredSourceProperty,
        string fallbackSourceProperty)
    {
        string sourceProperty = source != null && source.HasProperty(preferredSourceProperty)
            ? preferredSourceProperty
            : fallbackSourceProperty;
        Texture texture = GetTexture(source, sourceProperty);
        if (!destination.HasProperty(destinationProperty))
        {
            return;
        }

        if (texture != null)
        {
            destination.SetTexture(destinationProperty, texture);
        }
        destination.SetTextureScale(
            destinationProperty,
            GetTextureScale(source, sourceProperty, sourceProperty));
        destination.SetTextureOffset(
            destinationProperty,
            GetTextureOffset(source, sourceProperty, sourceProperty));
    }

    private static void CopyFloat(
        Material source,
        Material destination,
        string destinationProperty,
        string sourceProperty,
        float fallback)
    {
        if (!destination.HasProperty(destinationProperty))
        {
            return;
        }

        destination.SetFloat(
            destinationProperty,
            source != null && source.HasProperty(sourceProperty)
                ? source.GetFloat(sourceProperty)
                : fallback);
    }

    private static void CopyColor(
        Material source,
        Material destination,
        string destinationProperty,
        string sourceProperty,
        Color fallback)
    {
        if (destination.HasProperty(destinationProperty))
        {
            destination.SetColor(
                destinationProperty,
                GetColor(source, sourceProperty, fallback));
        }
    }

    private static void SetDefaultTexture(
        Material material,
        string propertyName,
        Texture texture)
    {
        if (material.HasProperty(propertyName) &&
            material.GetTexture(propertyName) == null)
        {
            material.SetTexture(propertyName, texture);
        }
    }
}
