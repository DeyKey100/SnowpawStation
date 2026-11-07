using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Взаимодействие по расстоянию вместо луча из камеры: на каждом интерактивном
/// объекте стоит ProximityInteractable, а здесь выбирается ближайший подошедший.
/// Плашка и нажатие [E] общие на всех — рядом стоящие объекты не спорят за клавишу.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Proximity")]
    [SerializeField] private string playerTag = "Player";

    private static readonly List<ProximityInteractable> markers = new List<ProximityInteractable>();

    /// <summary>Сколько метров близости «стоит» одна единица приоритета маркера.</summary>
    private const float PriorityMeters = 0.5f;

    private Transform playerTransform;
    private Camera viewCamera;
    private ProximityInteractable focused;
    private Texture2D plateBackground;

    /// <summary>Объект в радиусе с непустой подсказкой (дверные триггеры уступают ему [E]).</summary>
    public static bool HasFocusedInteractable { get; private set; }

    /// <summary>Объект, который сейчас показывает плашку (только для проверок и отладки).</summary>
    public static ProximityInteractable Focused { get; private set; }

    public static void RegisterMarker(ProximityInteractable marker)
    {
        if (marker == null || markers.Contains(marker))
        {
            return;
        }

        markers.Add(marker);
    }

    public static void UnregisterMarker(ProximityInteractable marker)
    {
        if (marker == null)
        {
            return;
        }

        markers.Remove(marker);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        markers.Clear();
        Focused = null;
        HasFocusedInteractable = false;
    }

    private void Awake()
    {
        plateBackground = MakeSolidTexture(new Color(0.02f, 0.03f, 0.04f, 0.82f));
    }

    private void OnDestroy()
    {
        if (plateBackground != null)
        {
            Destroy(plateBackground);
        }
    }

    private void Update()
    {
        RefreshPlayer();
        RefreshCamera();
        markers.RemoveAll(marker => marker == null);

        if (playerTransform == null)
        {
            ClearFocus();
            return;
        }

        // Диалог/мини-игра/заправка держат InteractionLock: мир не интерактивен.
        if (PauseMenu.IsPaused || InteractionLock.IsLocked || SceneLoadingScreen.IsLoading)
        {
            ClearFocus();
            return;
        }

        if (focused != null && (!focused.isActiveAndEnabled || !markers.Contains(focused)))
        {
            focused = null;
        }

        focused = FindNearest(playerTransform.position, out float focusedDistance);

        if (focused == null)
        {
            ClearFocus();
            return;
        }

        // Дверь ближе предмета — [E] и плашку отдаём двери, иначе записка на пороге
        // «съедала» нажатие и войти было нельзя.
        SceneDoorTrigger nearestDoor = SceneDoorTrigger.NearestInRange(playerTransform.position, out float doorDistance);
        if (nearestDoor != null && doorDistance < focusedDistance)
        {
            ClearFocus();
            return;
        }

        Focused = focused;
        HasFocusedInteractable = true;

        if (!InteractPressed())
        {
            return;
        }

        IInteractable target = focused.ResolveTarget();
        if (target != null)
        {
            target.Interact();
        }
    }

    /// <summary>Ближайший объект в радиусе, у которого есть непустая подсказка.</summary>
    private static ProximityInteractable FindNearest(Vector3 playerPosition, out float nearestDistance)
    {
        ProximityInteractable nearest = null;
        float bestScore = float.MaxValue;
        nearestDistance = float.MaxValue;

        for (int i = 0; i < markers.Count; i++)
        {
            ProximityInteractable marker = markers[i];
            if (marker == null || !marker.isActiveAndEnabled)
            {
                continue;
            }

            bool inRange = marker.TryGetRange(playerPosition, out float distance);
            marker.PlayerInRange = inRange;

            if (!inRange)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(marker.Prompt))
            {
                continue;
            }

            // Приоритет перевешивает близость: записка лежит на столе рядом со стойкой,
            // и без него подсказку кассы получал бы тот, кто стоит дальше от записки.
            float score = distance - marker.Priority * PriorityMeters;

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            nearest = marker;
            nearestDistance = distance;
        }

        return nearest;
    }

    private void ClearFocus()
    {
        focused = null;
        Focused = null;
        HasFocusedInteractable = false;
    }

    private void RefreshPlayer()
    {
        if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        playerTransform = playerObject != null ? playerObject.transform : transform;
    }

    private void RefreshCamera()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }
    }

    private bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return interactKey == KeyCode.E &&
               Keyboard.current != null &&
               Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(interactKey);
#endif
    }

    private void OnGUI()
    {
        if (focused == null || viewCamera == null || plateBackground == null || SceneLoadingScreen.IsLoading)
        {
            return;
        }

        string prompt = focused.Prompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        Vector3 screenPoint = viewCamera.WorldToScreenPoint(focused.CenterWorldPoint);
        if (screenPoint.z <= 0.1f)
        {
            // Объект за камерой — рисовать нечего.
            return;
        }

        // Отвернулись в сторону — плашки нет.
        const float sideMargin = 60f;
        if (screenPoint.x < -sideMargin || screenPoint.x > Screen.width + sideMargin)
        {
            return;
        }

        float x = screenPoint.x;

        // Камера висит высоко и смотрит вниз под 45°, поэтому «над объектом» в мире и
        // «над объектом» на экране — разные вещи. Привязываемся к экранной проекции
        // центра объекта и поднимаем плашку на фиксированное число пикселей.
        const float plateLiftPixels = 46f;
        float y = (Screen.height - screenPoint.y) - plateLiftPixels;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { background = plateBackground, textColor = Color.white }
        };

        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(14, 14, 0, 0),
            normal = { textColor = Color.white }
        };

        Vector2 textSize = textStyle.CalcSize(new GUIContent(prompt));
        float width = Mathf.Clamp(textSize.x + 30f, 170f, Screen.width - 32f);
        const float height = 44f;

        // Поджимаем к границам экрана, чтобы плашка не исчезала у краёв кадра.
        const float margin = 12f;
        x = Mathf.Clamp(x, width * 0.5f + margin, Screen.width - width * 0.5f - margin);
        y = Mathf.Clamp(y, height + margin, Screen.height - margin);

        Rect rect = new Rect(x - width * 0.5f, y - height, width, height);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(rect, prompt, textStyle);
    }

    private Texture2D MakeSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
