using System;
using UnityEngine;

public class FirewoodStorage : MonoBehaviour
{
    [SerializeField, Min(0)] private int firewoodCount;
    [SerializeField, Min(1)] private int maxFirewood = 3;

    public int FirewoodCount => firewoodCount;
    public int MaxFirewood => maxFirewood;
    public bool CanAddFirewood => firewoodCount < maxFirewood;
    public bool HasFirewood => firewoodCount > 0;

    public event Action<int, int> FirewoodChanged;

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
