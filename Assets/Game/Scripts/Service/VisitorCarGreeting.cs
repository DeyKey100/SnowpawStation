using System;
using UnityEngine;

/// <summary>
/// Приветствие посетителя показывается не в момент парковки, а когда игрок сам
/// подошёл к машине: иначе диалог всплывал через весь двор, пока игрок ещё
/// выходил из магазина. Компонент висит на корне машины, срабатывает один раз
/// и сразу снимается.
/// </summary>
[DisallowMultipleComponent]
public class VisitorCarGreeting : MonoBehaviour
{
    private const string PlayerTag = "Player";

    /// <summary>
    /// Насколько близко нужно подойти к борту машины. Меряется от габаритов корпуса,
    /// а не от пивота, поэтому 2.6 м — это уже «стою у машины», а не «прохожу мимо».
    /// </summary>
    private const float DefaultDistance = 2.6f;

    private const float VerticalLimit = 3f;

    private Action onApproach;
    private float activateDistance = DefaultDistance;
    private Transform player;
    private Collider body;

    /// <summary>Ждёт, пока игрок подойдёт к машине, и один раз вызывает onApproach.</summary>
    public static VisitorCarGreeting Attach(Transform carRoot, Action onApproach, float distance = DefaultDistance)
    {
        if (carRoot == null || onApproach == null)
        {
            return null;
        }

        VisitorCarGreeting greeting = carRoot.GetComponent<VisitorCarGreeting>();
        if (greeting == null)
        {
            greeting = carRoot.gameObject.AddComponent<VisitorCarGreeting>();
        }

        greeting.onApproach = onApproach;
        greeting.activateDistance = distance;
        greeting.body = carRoot.GetComponent<Collider>();
        greeting.player = null;
        greeting.enabled = true;
        return greeting;
    }

    private void Update()
    {
        if (onApproach == null)
        {
            enabled = false;
            return;
        }

        if (player == null || !player.gameObject.activeInHierarchy)
        {
            GameObject found = GameObject.FindGameObjectWithTag(PlayerTag);
            player = found != null ? found.transform : null;
            if (player == null)
            {
                return;
            }
        }

        if (!Reached(player.position))
        {
            return;
        }

        Action callback = onApproach;
        onApproach = null;
        enabled = false;
        callback();
    }

    /// <summary>Игрок подошёл: считаем по габаритам корпуса, а не по пивоту.</summary>
    private bool Reached(Vector3 playerPosition)
    {
        if (body == null)
        {
            body = GetComponent<Collider>();
        }

        Vector3 center = body != null ? body.bounds.center : transform.position;
        Vector3 size = body != null ? body.bounds.size : Vector3.one;

        float dx = Mathf.Max(Mathf.Abs(playerPosition.x - center.x) - size.x * 0.5f, 0f);
        float dz = Mathf.Max(Mathf.Abs(playerPosition.z - center.z) - size.z * 0.5f, 0f);
        float flat = Mathf.Sqrt(dx * dx + dz * dz);

        float dy = Mathf.Abs(playerPosition.y - center.y);
        return flat <= activateDistance && dy <= VerticalLimit;
    }
}
