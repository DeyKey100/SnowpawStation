using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Проход у двери: игрок приближается — над дверью появляется табличка,
/// по нажатию E грузится целевая сцена (через чёрный экран загрузки).
/// Работает по расстоянию, без луча-прицела, поэтому не конфликтует с PlayerInteraction.
///
/// E — клавиша общая с диалогом и предметами, поэтому порядок явный:
/// диалог (−200) → предметы (0) → дверь (100). Дверь замыкает очередь и последней
/// может забрать нажатие, а если его уже разобрали выше, молчит: без этого E,
/// которым закрывали реплику, в том же кадре уводило в другую сцену.
/// </summary>
[DefaultExecutionOrder(100)]
public class SceneDoorTrigger : MonoBehaviour
{
    /// <summary>Все двери в сцене: PlayerInteraction уступает им [E], если дверь ближе.</summary>
    private static readonly System.Collections.Generic.List<SceneDoorTrigger> doors =
        new System.Collections.Generic.List<SceneDoorTrigger>();
    [Header("Text")]
    [SerializeField] private string promptText = "E - войти";
    [SerializeField] private float plateWorldOffsetY = 0.35f;

    [Header("Target scene")]
    [SerializeField] private string targetSceneName = "";
    [SerializeField] private bool movePlayerToSpawn = false;
    [SerializeField] private Vector3 spawnPosition = Vector3.zero;
    [SerializeField] private float spawnYaw = 0f;

    [Header("Proximity")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float activateDistance = 2.2f;
    [SerializeField] private float maxVerticalDifference = 3f;
    [SerializeField] private float inputLockDuration = 0.35f;

    private Transform playerTransform;
    private Camera viewCamera;
    private bool playerInRange;
    private float inputUnlockTime;
    private Texture2D plateBackground;

    public bool PlayerInRange => playerInRange;

    /// <summary>Открытка Котова ещё лежит на пороге — дверь не активна.</summary>
    private static bool LetterWaitingOnDoorstep
    {
        get
        {
            KotovVisit kotov = StoryDirector.Instance != null ? StoryDirector.Instance.Kotov : null;
            return kotov != null && kotov.IsLetterWaiting;
        }
    }

    private void Awake()
    {
        plateBackground = MakeSolidTexture(new Color(0.02f, 0.03f, 0.04f, 0.82f));
        inputUnlockTime = Time.unscaledTime + inputLockDuration;
        doors.Add(this);
    }

    private void OnEnable()
    {
        if (!doors.Contains(this))
        {
            doors.Add(this);
        }
    }

    private void OnDisable()
    {
        doors.Remove(this);
    }

    /// <summary>Ближайшая к игроку дверь в радиусе (или null).</summary>
    public static SceneDoorTrigger NearestInRange(Vector3 playerPosition, out float distance)
    {
        SceneDoorTrigger nearest = null;
        distance = float.MaxValue;

        for (int i = doors.Count - 1; i >= 0; i--)
        {
            SceneDoorTrigger door = doors[i];
            if (door == null)
            {
                doors.RemoveAt(i);
                continue;
            }

            if (!door.playerInRange)
            {
                continue;
            }

            Vector3 delta = playerPosition - door.transform.position;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            if (horizontal < distance)
            {
                nearest = door;
                distance = horizontal;
            }
        }

        return nearest;
    }

    private void OnDestroy()
    {
        doors.Remove(this);

        if (plateBackground != null)
        {
            Destroy(plateBackground);
        }
    }

    private void Update()
    {
        RefreshPlayer();
        RefreshCamera();

        if (playerTransform == null)
        {
            playerInRange = false;
            return;
        }

        Vector3 delta = playerTransform.position - transform.position;
        float horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
        playerInRange = horizontalDistance <= activateDistance &&
                        Mathf.Abs(delta.y) <= maxVerticalDifference;

        if (!playerInRange || !InteractPressed() || Time.unscaledTime < inputUnlockTime)
        {
            return;
        }

        if (SceneLoadingScreen.IsLoading)
        {
            return;
        }

        // Диалог важнее двери: пока окно открыто, E листает реплики и не уводит в сцену.
        // Проверяем сам факт диалога, а не только InteractionLock: замок снимается
        // отложенно, и в кадре закрытия окна дверь успевала сработать.
        if (DialogueManager.IsActive)
        {
            return;
        }

        // Нажатие уже разобрали: диалог (он выше по порядку) или предмет под ногами.
        // Без этой проверки E, которым закрывали реплику, в том же кадре уводило в дверь.
        if (InputPriority.IsConsumed)
        {
            return;
        }

        // У порога может лежать открытка/записка: если предмет ближе, чем дверь,
        // [E] отдаём ему. Если ближе дверь — открываемся (иначе плашка предмета
        // «съедала» нажатие и дверь не работала вовсе).
        if (InteractionLock.IsLocked)
        {
            return;
        }

        if (PlayerInteraction.Focused != null)
        {
            if (!PlayerInteraction.Focused.TryGetRange(playerTransform.position, out float markerDistance) ||
                markerDistance < horizontalDistance)
            {
                return;
            }
        }

        // Открытка ещё не подобрана — дверь не открывается.
        if (LetterWaitingOnDoorstep)
        {
            return;
        }

        if (!InputPriority.TryConsume())
        {
            return;
        }

        if (movePlayerToSpawn)
        {
            SceneLoadingScreen.Load(targetSceneName, spawnPosition, spawnYaw);
        }
        else
        {
            SceneLoadingScreen.Load(targetSceneName);
        }
    }

    private void RefreshPlayer()
    {
        if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        playerTransform = playerObject != null ? playerObject.transform : null;
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
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private void OnGUI()
    {
        if (!playerInRange || viewCamera == null || plateBackground == null || SceneLoadingScreen.IsLoading)
        {
            return;
        }

        // Пока идёт диалог, табличка двери не висит: игрок читает реплики, а не выбирает сцену.
        if (DialogueManager.IsActive)
        {
            return;
        }

        // Пока на пороге лежит открытка Котова, дверь не предлагает войти:
        // сначала подбираем письмо, иначе [E] уводил в другую сцену.
        if (LetterWaitingOnDoorstep)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(promptText))
        {
            return;
        }

        Vector3 worldPoint = transform.position + Vector3.up * plateWorldOffsetY;
        // Камера может рендерить в маленький буфер PS1-эффекта, а IMGUI живёт
        // в координатах полного экрана — пересчитываем через viewport.
        Vector3 screenPoint = PsxLowResolution.WorldToScreenPoint(viewCamera, worldPoint);
        Rect frame = PsxLowResolution.ScreenRect;
        if (screenPoint.z <= 0.1f)
        {
            // Табличка за камерой — рисовать нечего.
            return;
        }

        // Табличка привязана к точке двери, поэтому при взгляде в сторону уезжает за край экрана.
        const float offscreenMargin = 80f;
        if (screenPoint.x < frame.xMin - offscreenMargin || screenPoint.x > frame.xMax + offscreenMargin ||
            screenPoint.y < -offscreenMargin || screenPoint.y > frame.height + offscreenMargin)
        {
            return;
        }

        float x = screenPoint.x;
        float y = frame.yMax - screenPoint.y;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { background = plateBackground, textColor = Color.white }
        };

        GUIStyle textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = UiTheme.Font,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(14, 14, 0, 0),
            normal = { textColor = Color.white }
        };

        Vector2 textSize = textStyle.CalcSize(new GUIContent(promptText));
        float width = Mathf.Clamp(textSize.x + 30f, 170f, frame.width - 32f);
        const float height = 44f;

        Rect rect = new Rect(x - width * 0.5f, y - height, width, height);
        GUI.Box(rect, GUIContent.none, boxStyle);
        GUI.Label(rect, promptText, textStyle);
    }

    private Texture2D MakeSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, activateDistance);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * plateWorldOffsetY);
    }
}
