#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Делает фары и стоп-сигналы машин посетителей горящими.
///
/// Почему Unlit, а не свечение: у URP/Lit свечение включается ключевым словом
/// `_EMISSION`, а оно отказалось сохраняться в этом проекте — и через
/// `EnableKeyword`, и через сериализованное `m_ValidKeywords` слово пропадало
/// при перезагрузке ассета, хотя в YAML лежало. Unlit вообще не участвует в
/// освещении: он рисует ровно свой цвет, поэтому лампа горит и днём, и её
/// нельзя «погасить» отсутствием ключевого слова.
///
/// Материалы фонарей пересобираются после каждого импорта модели: импорт заново
/// применяет описание материалов из FBX и возвращает URP/Lit.
/// </summary>
public sealed class NpcCarLightMaterials : AssetPostprocessor
{
    private const string NpcCarsRoot = "/NpcCars/";
    private const string UnlitShaderName = "Universal Render Pipeline/Unlit";

    private static readonly Color HeadlightColor = new Color(1f, 0.93f, 0.72f);
    private static readonly Color TaillightColor = new Color(0.95f, 0.09f, 0.06f);
    private static readonly Color MarkerLightColor = new Color(1f, 0.66f, 0.16f);

    /// <summary>
    /// Карта «модель → материал → цвет». Выписана из FBX вручную: там у всех
    /// фонарей один белый, а принадлежность к носу/корме определялась по средней
    /// координате Z детали (нос модели смотрит в +Z).
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, Color>> Lights =
        new Dictionary<string, Dictionary<string, Color>>
        {
            {
                "hatchback", new Dictionary<string, Color>
                {
                    { "Material_17", HeadlightColor },
                    { "Material_18", HeadlightColor },
                    { "Material_20", TaillightColor },
                }
            },
            {
                "jeep", new Dictionary<string, Color>
                {
                    { "Material_14", HeadlightColor },
                    { "Material_15", HeadlightColor },
                    { "Material_16", HeadlightColor },
                    { "Material_21", TaillightColor },
                }
            },
            {
                "pickup", new Dictionary<string, Color>
                {
                    { "Material_19", HeadlightColor },
                    { "Material_20", HeadlightColor },
                    { "Material_22", TaillightColor },
                    { "Material_25", MarkerLightColor },
                }
            },
            {
                "truck1", new Dictionary<string, Color>
                {
                    { "Material_14", HeadlightColor },
                    { "Material_15", HeadlightColor },
                    { "Material_16", HeadlightColor },
                    { "Material_18", TaillightColor },
                }
            },
            {
                // У фургона «Почта» фары спереди (z = +2.41), стопы сзади: цвета
                // в модели уже были правильные, не горели они только из-за
                // выключенного ключевого слова.
                "van-pochta", new Dictionary<string, Color>
                {
                    { "Material_14", HeadlightColor },
                    { "Material_15", HeadlightColor },
                    { "Material_17", TaillightColor },
                    { "Material_18", TaillightColor },
                }
            },
        };

    private void OnPostprocessModel(GameObject model)
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains(NpcCarsRoot))
        {
            return;
        }

        string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
        if (folder == null)
        {
            return;
        }

        ApplyToModel(folder.ToLowerInvariant(), System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
    }

    /// <summary>
    /// Пересобирает материалы фонарей для всех моделей. Вызывается из
    /// NpcCarTextureBinder в конце: его SaveAndReimport перезаписывает .mat уже
    /// после того, как постпроцессор отработал.
    /// </summary>
    public static void ApplyAll()
    {
        foreach (var pair in Lights)
        {
            ApplyToModel(pair.Key, "Assets/NpcCars/" + pair.Key);
        }

        AssetDatabase.SaveAssets();
    }

    private static void ApplyToModel(string modelKey, string modelFolder)
    {
        Dictionary<string, Color> lights;
        if (!Lights.TryGetValue(modelKey, out lights))
        {
            return;
        }

        var unlit = Shader.Find(UnlitShaderName);
        if (unlit == null)
        {
            Debug.LogWarning("Фонари машин: не найден шейдер " + UnlitShaderName);
            return;
        }

        string materialsFolder = modelFolder + "/Materials";
        foreach (var pair in lights)
        {
            string materialPath = materialsFolder + "/" + pair.Key + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                continue;
            }

            // Шейдер меняем, а не только цвет: у URP/Lit свечение зависит от
            // ключевого слова, которое здесь не сохраняется.
            if (material.shader != unlit)
            {
                material.shader = unlit;
            }

            material.SetColor("_BaseColor", pair.Value);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Cull", 2f);
            EditorUtility.SetDirty(material);
        }
    }
}
#endif
