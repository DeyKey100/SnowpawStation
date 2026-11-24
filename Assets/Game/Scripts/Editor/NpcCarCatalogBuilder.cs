#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Собирает каталог моделей машин в Resources, чтобы они попадали в билд.
///
/// Сами fbx лежат в Assets/NpcCars — вне Resources, поэтому в собранную игру они
/// не попадают, и посетители приезжали на заглушке. Каталог держит прямые ссылки
/// на модели: билд тянет их вместе с ним. Пересобирать после добавления новой
/// машины в NpcCarModels.VisitorPaths.
/// </summary>
public static class NpcCarCatalogBuilder
{
    private const string CatalogFolder = "Assets/Game/Resources";
    private const string CatalogPath = CatalogFolder + "/NpcCarCatalog.asset";

    [MenuItem("Tools/Snowpaw/Собрать каталог машин")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<NpcCarCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder(CatalogFolder))
            {
                AssetDatabase.CreateFolder("Assets/Game", "Resources");
            }

            catalog = ScriptableObject.CreateInstance<NpcCarCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var paths = new List<string>(NpcCarModels.VisitorPaths) { NpcCarModels.PlaceholderPath };
        var entries = new List<NpcCarCatalog.Entry>();
        int missing = 0;

        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                missing++;
                Debug.LogWarning("Каталог машин: модель не найдена — " + path);
                continue;
            }

            entries.Add(new NpcCarCatalog.Entry { path = path, prefab = prefab });
        }

        catalog.entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format("Каталог машин: {0} моделей, не найдено — {1}.", entries.Count, missing));
    }
}
#endif
