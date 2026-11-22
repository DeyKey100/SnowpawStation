#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Привязывает к материалам машин посетителей картинки, распакованные из FBX,
/// и превращает фонари в реальные .mat-ассеты.
///
/// Почему не импортёр: внутри FBX картинки прописаны абсолютными путями на чужой
/// диск (G:\Мой диск\Igra-PD\...). Unity такие пути не находит и молча создаёт
/// однотонные материалы. Зато имена материалов внутри модели устойчивы
/// (Material_0, Material_1, ...), поэтому связка «материал → картинка» берётся из
/// самого FBX скриптом Tools/fbx_textures.py, и он же кладёт картинки под этими
/// именами в папку &lt;модель&gt;.fbm.
///
/// Скрипт запускается из меню. Цвет свечения фар здесь не выставляется — это
/// делает NpcCarLightMaterials после каждого импорта модели, иначе импорт
/// перезаписывает .mat и гасит фонари.
/// </summary>
public static class NpcCarTextureBinder
{
    private const string ModelsRoot = "Assets/NpcCars";
    private const string TextureFolderSuffix = ".fbm";
    private const string MaterialsFolder = "Materials";
    private const string ShaderName = "Universal Render Pipeline/Lit";

    /// <summary>
    /// Какие материалы модели — фонари. Выписано из самого FBX по средней
    /// координате Z детали (нос модели смотрит в +Z). Автоопределение по цвету
    /// свечения не работает: у URP/Lit белый _EmissionColor стоит по умолчанию у
    /// любого материала, так что светящимся оказалось бы всё.
    /// </summary>
    private static readonly Dictionary<string, string[]> LightMaterials =
        new Dictionary<string, string[]>
        {
            { "hatchback", new[] { "Material_17", "Material_18", "Material_20" } },
            { "jeep", new[] { "Material_14", "Material_15", "Material_16", "Material_21" } },
            { "pickup", new[] { "Material_19", "Material_20", "Material_22", "Material_25" } },
            { "truck1", new[] { "Material_14", "Material_15", "Material_16", "Material_18" } },
            { "van-pochta", new[] { "Material_14", "Material_15", "Material_17", "Material_18" } },
        };

    [MenuItem("Tools/Snowpaw/Привязать текстуры машин NPC")]
    public static void BindAll()
    {
        int models = 0, materials = 0, lights = 0;

        foreach (string fbx in Directory.GetFiles(ModelsRoot, "*.fbx", SearchOption.AllDirectories))
        {
            models++;
            materials += BindModel(fbx.Replace('\\', '/'), ref lights);
        }

        AssetDatabase.SaveAssets();

        // Свечение — последним шагом: SaveAndReimport выше перезаписывает .mat и
        // снимает ключевое слово _EMISSION, поэтому включать его раньше бессмысленно.
        NpcCarLightMaterials.ApplyAll();

        Debug.Log(string.Format(
            "Машины: моделей — {0}, материалов с картинкой — {1}, фонарей — {2}.",
            models, materials, lights));
    }

    private static int BindModel(string modelPath, ref int lights)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        if (importer == null)
        {
            return 0;
        }

        string textureFolder = Path.ChangeExtension(modelPath, TextureFolderSuffix);
        string modelFolder = Path.GetDirectoryName(modelPath);
        string materialsFolder = Path.Combine(modelFolder, MaterialsFolder).Replace('\\', '/');
        string modelKey = Path.GetFileName(modelFolder).ToLowerInvariant();
        EnsureFolder(materialsFolder);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            return 0;
        }

        string[] lightNames;
        if (!LightMaterials.TryGetValue(modelKey, out lightNames))
        {
            lightNames = new string[0];
        }

        // Материалы берём с рендереров, а не из LoadAllAssetsAtPath: после первого
        // прогона часть из них уже подменена нашими .mat, и список встроенных
        // пустеет — из-за этого повторный запуск ничего не делал.
        var used = new Dictionary<string, Material>();
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null)
                {
                    used[material.name] = material;
                }
            }
        }

        int bound = 0;
        foreach (var pair in used)
        {
            string materialName = pair.Key;
            string texturePath = Path.Combine(textureFolder, materialName + ".png").Replace('\\', '/');
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            bool isLight = System.Array.IndexOf(lightNames, materialName) >= 0;

            if (texture == null && !isLight)
            {
                continue;
            }

            string materialPath = materialsFolder + "/" + materialName + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find(ShaderName));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            if (texture != null)
            {
                material.SetTexture("_BaseMap", texture);

                // В модели у материала зашит серый диффузный цвет-подложка. Если его
                // оставить, текстура умножается на серый и выглядит грязной.
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.15f);
                bound++;
            }

            if (isLight)
            {
                // Сама лампа чёрная: светится только свечение, иначе краска вокруг
                // фонаря забивает цвет.
                material.SetColor("_BaseColor", Color.black);
                material.SetFloat("_Smoothness", 0.8f);
                lights++;
            }

            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName), material);
        }

        importer.SaveAndReimport();
        return bound;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
#endif
