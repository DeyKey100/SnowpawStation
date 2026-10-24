using UnityEngine;

public class FirewoodStorage : MonoBehaviour
{
    [SerializeField, Min(0)] private int firewoodCount;
    [SerializeField, Min(1)] private int maxFirewood = 3;

    public int FirewoodCount => firewoodCount;

    public bool TryAddFirewood()
    {
        if (firewoodCount >= maxFirewood)
        {
            Debug.Log("Запас дров заполнен.", this);
            return false;
        }

        firewoodCount++;
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
        Debug.Log($"[Snowpaw Station] Дров в запасе: {firewoodCount}/{maxFirewood}.", this);
        return true;
    }
}
