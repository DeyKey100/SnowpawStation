using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Готовые меши и материалы товаров из Resources/ShopGoods.
/// Применяются только к нашим заглушкам: произвольный авторский визуал не заменяем.
/// </summary>
public static class ShopGoodVisuals
{
    private const string ResourceFolder = "ShopGoods/";
    private const string ErrorShaderName = "Hidden/InternalErrorShader";
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly Dictionary<string, VisualResources> Cache =
        new Dictionary<string, VisualResources>(6);
    private static Mesh cube;
    private static Mesh cylinder;

    private readonly struct VisualResources
    {
        public readonly Mesh Mesh;
        public readonly Material Material;

        public VisualResources(Mesh mesh, Material material)
        {
            Mesh = mesh;
            Material = material;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        Cache.Clear();
        cube = null;
        cylinder = null;
    }

    /// <summary>Можно ли менять визуал и нормализованный масштаб этого товара.</summary>
    public static bool CanManage(GameObject item, string goodId, bool created = false)
    {
        if (item == null || string.IsNullOrEmpty(goodId) ||
            !item.TryGetComponent(out MeshFilter filter) ||
            !item.TryGetComponent(out MeshRenderer renderer))
        {
            return false;
        }

        if (created)
        {
            return true;
        }

        Mesh mesh = filter.sharedMesh;
        if (mesh == null)
        {
            return false;
        }

        cube ??= Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        cylinder ??= Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        bool primitive = mesh == cube || mesh == cylinder;
        // Unity uses the asset filename as its loaded main-object name.
        bool managed = mesh.name == "ShopGood_" + goodId + "_Mesh" ||
                       mesh.name == goodId + "_Mesh";
        if (!primitive && !managed)
        {
            return false;
        }

        Material material = renderer.sharedMaterial;
        if (material == null || material.shader == null || material.shader.name == ErrorShaderName)
        {
            return true;
        }

        if (HasGeneratedName(material.name, "ShopGood_" + goodId + "_Material") ||
            HasGeneratedName(material.name, goodId + "_Material"))
        {
            return true;
        }

        // Старые одноцветные примитивы сцены. Текстурированный либо иначе названный
        // материал считаем авторским, даже если он использует стандартный Cube.
        Texture texture = GetBaseTexture(material);
        bool untextured = texture == null || texture == Texture2D.whiteTexture;
        return primitive && untextured &&
               (HasGeneratedName(material.name, "Lit") ||
                HasGeneratedName(material.name, "Default-Material") ||
                HasGeneratedName(material.name, item.name + "_Display") ||
                HasGeneratedName(material.name, "ShopDisplay_" + item.name) ||
                (goodId == "coffee" && HasGeneratedName(material.name, "ShopCoffeeCup")));
    }

    /// <summary>Подставить цельную Resource-пару без клонирования и перекраски материала.</summary>
    public static bool TryApply(GameObject item, string goodId, bool created = false)
    {
        if (!CanManage(item, goodId, created) || !TryLoad(goodId, out VisualResources resources))
        {
            return false;
        }

        // Компоненты уже проверены CanManage; этот метод не вызывается из Update.
        item.TryGetComponent(out MeshFilter filter);
        item.TryGetComponent(out MeshRenderer renderer);
        filter.sharedMesh = resources.Mesh;
        renderer.sharedMaterial = resources.Material;
        return true;
    }

    private static bool TryLoad(string goodId, out VisualResources resources)
    {
        if (Cache.TryGetValue(goodId, out resources) &&
            resources.Mesh != null && resources.Material != null &&
            resources.Material.shader != null && resources.Material.shader.name != ErrorShaderName &&
            GetBaseTexture(resources.Material) != null)
        {
            return true;
        }

        string path = ResourceFolder + goodId;
        Mesh mesh = Resources.Load<Mesh>(path + "_Mesh");
        Material material = Resources.Load<Material>(path + "_Material");
        if (mesh == null || material == null || material.shader == null ||
            material.shader.name == ErrorShaderName || GetBaseTexture(material) == null)
        {
            // Не кешируем отсутствие ресурсов: в Editor генератор может создать их позже.
            resources = default;
            return false;
        }

        resources = new VisualResources(mesh, material);
        Cache[goodId] = resources;
        return true;
    }

    private static Texture GetBaseTexture(Material material)
    {
        // PSX-шейдер использует _BaseMap без атрибута MainTexture.
        // Material.mainTexture поэтому не гарантирует доступ к его текстуре.
        if (material.HasProperty(BaseMapId))
        {
            return material.GetTexture(BaseMapId);
        }

        return material.HasProperty(MainTextureId) ? material.GetTexture(MainTextureId) : null;
    }

    private static bool HasGeneratedName(string name, string expected)
    {
        // PSX-конвертация и Renderer.material могут добавлять только эти суффиксы.
        if (!name.StartsWith(expected, System.StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = name.Substring(expected.Length);
        suffix = suffix.Replace("_PSXVertexSnap", "").Replace(" (Instance)", "");
        return suffix.Length == 0;
    }
}
