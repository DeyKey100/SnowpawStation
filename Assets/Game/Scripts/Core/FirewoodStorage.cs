using System;
using UnityEngine;

public class FirewoodStorage : MonoBehaviour
{
    [SerializeField, Min(0)] private int firewoodCount;
    [SerializeField, Min(1)] private int maxFirewood = 3;

    public static FirewoodStorage Active { get; private set; }

    public int FirewoodCount => firewoodCount;
    public int CurrentFirewood => firewoodCount;
    public int MaxFirewood => maxFirewood;
    public bool CanAddFirewood => firewoodCount < maxFirewood;
    public bool HasFirewood => firewoodCount > 0;

    public event Action<int, int> FirewoodChanged;

    private void Awake()
    {
        // После смены сцен DontDestroyOnLoad-дубль сцены даёт второй экземпляр: лишний самоудаляется.
        if (Active != null && Active != this)
        {
            Debug.LogWarning("[Snowpaw Station] Дубликат FirewoodStorage уничтожен при перезагрузке сцены.", this);
            Destroy(gameObject);
            return;
        }

        Active = this;
    }

    private void OnDestroy()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    private void Start()
    {
        FirewoodChanged?.Invoke(firewoodCount, maxFirewood);
    }

    public bool TryAddFirewood()
    {
        if (firewoodCount >= maxFirewood)
        {
            Debug.Log("Запас дров заполнен.", this);
            return false;
        }

        firewoodCount++;
        FirewoodChanged?.Invoke(firewoodCount, maxFirewood);
        Debug.Log($"[Snowpaw Station] Дров в запасе: {firewoodCount}/{maxFirewood}.", this);
        return true;
    }

    public bool TryConsumeFirewood()
    {
        if (firewoodCount <= 0)
        {
            Debug.Log("Нет дров для печи.", this);
            return false;
        }

        firewoodCount--;
        FirewoodChanged?.Invoke(firewoodCount, maxFirewood);
        Debug.Log($"[Snowpaw Station] Дров в запасе: {firewoodCount}/{maxFirewood}.", this);
        return true;
    }
}
