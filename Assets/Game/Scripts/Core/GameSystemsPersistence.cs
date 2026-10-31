using UnityEngine;

/// <summary>
/// Держит GameSystems (день, тепло, дрова) живым при смене сцен.
/// </summary>
public class GameSystemsPersistence : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
