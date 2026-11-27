using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Меню для заготовленных точек сугробов: создать стартовый набор в открытой сцене
/// (по координатам из SnowPileSpawner) и посмотреть, какие точки уже расставлены.
/// </summary>
public static class SnowSpawnPointTools
{
    private const string StationSceneName = "StationMain";
    private const string CreateMenuPath = "Snowpaw/Snow Piles/Create Default Spawn Points";
    private const string ReportMenuPath = "Snowpaw/Snow Piles/Report Spawn Points";

    [MenuItem(CreateMenuPath)]
    private static void CreateDefaultSpawnPoints()
    {
        Scene scene = SceneManager.GetActiveScene();
        SnowSpawnPoint[] existing = FindPoints();
        if (existing.Length > 0)
        {
            EditorUtility.DisplayDialog("Snow spawn points",
                $"В сцене уже есть точки: {existing.Length}. Удалите их, если нужно пересоздать набор по умолчанию.", "OK");
            Selection.activeGameObject = existing[0].gameObject;
            return;
        }

        if (scene.name != StationSceneName && !EditorUtility.DisplayDialog("Snow spawn points",
                $"Активная сцена — {scene.name}, а сугробы спавнятся в {StationSceneName}. Всё равно создать точки здесь?",
                "Создать", "Отмена"))
        {
            return;
        }

        GameObject root = new GameObject("SnowSpawnPoints");
        Undo.RegisterCreatedObjectUndo(root, "Create snow spawn points");
        List<SnowSpawnPoint> created = new List<SnowSpawnPoint>();
        SnowPileSpawner.PopulateDefaultPoints(root.transform, created);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Snowpaw Station] Создано точек: {created.Count}. Расставьте их в Scene View и сохраните сцену (Ctrl+S).", root);
    }

    [MenuItem(ReportMenuPath)]
    private static void ReportSpawnPoints()
    {
        SnowSpawnPoint[] points = FindPoints();
        if (points.Length == 0)
        {
            Debug.Log("[Snowpaw Station] В открытых сценах нет SnowSpawnPoint: в игре будет использован набор по умолчанию из SnowPileSpawner.");
            return;
        }

        StringBuilder report = new StringBuilder();
        report.Append("[Snowpaw Station] Точек сугробов: ").Append(points.Length).AppendLine();
        for (int i = 0; i < points.Length; i++)
        {
            SnowSpawnPoint point = points[i];
            report.Append("  ").Append(point.name)
                .Append(point.IsActive ? "" : " (выключена)")
                .Append(point.HasTask ? " задача: " + point.TaskId : " свободная")
                .Append(" @ ").Append(point.transform.position).AppendLine();
        }
        Debug.Log(report.ToString());
    }

    private static SnowSpawnPoint[] FindPoints()
    {
        return Object.FindObjectsByType<SnowSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
    }
}
