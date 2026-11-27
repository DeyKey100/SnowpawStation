using UnityEngine;

/// <summary>
/// Заготовленная точка для сугроба в StationMain. Расставляется в сцене руками
/// (стартовый набор создаёт меню Snowpaw/Snow Piles), SnowPileSpawner в начале каждого
/// дня занимает часть точек. Пустой taskId — свободная точка для «визуальных» сугробов,
/// заполненный (snow_door, snow_pump1, snow_yard) — точка для обязательной утренней задачи.
/// </summary>
[DisallowMultipleComponent]
public sealed class SnowSpawnPoint : MonoBehaviour
{
    [Tooltip("Выключенная точка остаётся в сцене, но в спавне не участвует")]
    [SerializeField] private bool isActive = true;

    [Tooltip("Id утренней задачи, под которую зарезервирована точка (snow_door, snow_pump1, snow_yard). Пусто — свободная точка")]
    [SerializeField] private string taskId = "";

    public bool IsActive => isActive;

    public string TaskId => taskId;

    public bool HasTask => !string.IsNullOrEmpty(taskId);

    public bool ServesTask(string id)
    {
        return HasTask && string.Equals(taskId, id, System.StringComparison.Ordinal);
    }

    public void Configure(string assignedTaskId, bool active = true)
    {
        taskId = assignedTaskId ?? "";
        isActive = active;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = GizmoColor(1f);
        Vector3 position = transform.position;
        Gizmos.DrawWireCube(CheckCenter(position), SnowPileSpawner.CheckHalfExtents * 2f);
        Gizmos.DrawLine(position, position + Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = GizmoColor(0.25f);
        Gizmos.DrawCube(CheckCenter(transform.position), SnowPileSpawner.CheckHalfExtents * 2f);
    }

    private Color GizmoColor(float alpha)
    {
        if (!isActive)
        {
            return new Color(0.5f, 0.5f, 0.5f, alpha);
        }
        return HasTask ? new Color(1f, 0.75f, 0.2f, alpha) : new Color(0.55f, 0.85f, 1f, alpha);
    }

    private static Vector3 CheckCenter(Vector3 position)
    {
        return new Vector3(position.x, SnowPileSpawner.GroundY + 0.26f, position.z);
    }
}
