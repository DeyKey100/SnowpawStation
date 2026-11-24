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

    /// <summary>
    /// Цвета однотонных деталей (стекло, шины, диски, кузовные вставки), выписанные
    /// из самого FBX: свойства DiffuseColor материала. Картинки у них нет, и без
    /// этой таблицы они остаются чёрными с глянцем — на солнце такой чёрный
    /// выглядит белым бликом, особенно на колёсах фургона.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, Color>> MatteColors =
        new Dictionary<string, Dictionary<string, Color>>
        {
            { "hatchback", new Dictionary<string, Color>
                {
                    { "Material_3", new Color(0.0160f, 0.0160f, 0.0160f) },
                    { "Material_10", new Color(0.7605f, 0.2051f, 0.0110f) },
                    { "Material_11", new Color(0.0232f, 0.1590f, 0.4621f) },
                    { "Material_12", new Color(0.0482f, 0.0685f, 0.0423f) },
                    { "Material_13", new Color(0.0103f, 0.0103f, 0.0103f) },
                    { "Material_14", new Color(0.0056f, 0.0056f, 0.0056f) },
                    { "Material_15", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_16", new Color(0.0070f, 0.0070f, 0.0080f) },
                    { "Material_22", new Color(0.2462f, 0.2623f, 0.2874f) },
                    { "Material_25", new Color(0.4564f, 0.4793f, 0.5271f) },
                }
            },
            { "jeep", new Dictionary<string, Color>
                {
                    { "Material_3", new Color(0.0160f, 0.0160f, 0.0160f) },
                    { "Material_10", new Color(0.0194f, 0.0194f, 0.0232f) },
                    { "Material_11", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_12", new Color(0.0070f, 0.0070f, 0.0080f) },
                    { "Material_13", new Color(0.6308f, 0.6308f, 0.5271f) },
                    { "Material_17", new Color(0.0075f, 0.0075f, 0.0103f) },
                    { "Material_18", new Color(0.0103f, 0.0103f, 0.0103f) },
                    { "Material_24", new Color(0.4564f, 0.4793f, 0.5271f) },
                    { "Material_29", new Color(0.3231f, 0.3515f, 0.3916f) },
                    { "Material_30", new Color(0.1441f, 0.0685f, 0.0232f) },
                }
            },
            { "pickup", new Dictionary<string, Color>
                {
                    { "Material_3", new Color(0.0160f, 0.0160f, 0.0160f) },
                    { "Material_6", new Color(0.0194f, 0.0194f, 0.0232f) },
                    { "Material_12", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_14", new Color(0.4342f, 0.0232f, 0.0160f) },
                    { "Material_16", new Color(0.5841f, 0.6038f, 0.6445f) },
                    { "Material_17", new Color(0.0070f, 0.0070f, 0.0080f) },
                    { "Material_18", new Color(0.6308f, 0.6308f, 0.5271f) },
                    { "Material_24", new Color(0.2462f, 0.2623f, 0.2874f) },
                    { "Material_26", new Color(0.0103f, 0.0103f, 0.0103f) },
                    { "Material_29", new Color(0.4564f, 0.4793f, 0.5271f) },
                }
            },
            { "truck1", new Dictionary<string, Color>
                {
                    { "Material_0", new Color(0.0194f, 0.0194f, 0.0232f) },
                    { "Material_4", new Color(0.0160f, 0.0160f, 0.0160f) },
                    { "Material_11", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_12", new Color(0.0070f, 0.0070f, 0.0080f) },
                    { "Material_13", new Color(0.6867f, 0.6867f, 0.6867f) },
                    { "Material_20", new Color(0.0103f, 0.0103f, 0.0103f) },
                    { "Material_23", new Color(0.4564f, 0.4793f, 0.5271f) },
                }
            },
            { "van-pochta", new Dictionary<string, Color>
                {
                    { "Material_9", new Color(0.0685f, 0.0685f, 0.0844f) },
                    { "Material_10", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_11", new Color(0.3231f, 0.3231f, 0.3231f) },
                    { "Material_12", new Color(0.3231f, 0.3231f, 0.3231f) },
                    { "Material_13", new Color(0.3231f, 0.3231f, 0.3231f) },
                    { "Material_16", new Color(0.0685f, 0.0685f, 0.0844f) },
                    { "Material_19", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_20", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_21", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_22", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_23", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_24", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_25", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_26", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_27", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_28", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_29", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_30", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_31", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_32", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_33", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_34", new Color(0.5841f, 0.3968f, 0.1441f) },
                    { "Material_35", new Color(0.0232f, 0.0232f, 0.0273f) },
                    { "Material_36", new Color(0.0075f, 0.0075f, 0.0086f) },
                    { "Material_37", new Color(0.3231f, 0.3231f, 0.3231f) },
                    { "Material_38", new Color(0.0075f, 0.0075f, 0.0086f) },
                    { "Material_39", new Color(0.3231f, 0.3231f, 0.3231f) },
                    { "Material_40", new Color(0.0075f, 0.0075f, 0.0086f) },
                    { "Material_41", new Color(0.3231f, 0.3231f, 0.3231f) },
                    { "Material_42", new Color(0.0075f, 0.0075f, 0.0086f) },
                    { "Material_43", new Color(0.3231f, 0.3231f, 0.3231f) },
                }
            },
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

        Dictionary<string, Color> matteColors;
        if (!MatteColors.TryGetValue(modelKey, out matteColors))
        {
            matteColors = new Dictionary<string, Color>();
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

            Color matteColor;
            bool hasMatte = matteColors.TryGetValue(materialName, out matteColor);

            if (texture == null && !isLight && !hasMatte)
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
            else if (hasMatte && !isLight)
            {
                // Деталь без картинки (стекло, шины, диски): цвет берём из FBX.
                // Оставлять её чёрной с гладкостью 0.8 нельзя — такой чёрный
                // зеркалит небо и читается как белое пятно.
                material.SetColor("_BaseColor", matteColor);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.1f);
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
