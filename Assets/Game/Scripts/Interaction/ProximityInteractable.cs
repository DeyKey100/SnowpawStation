using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Взаимодействие по расстоянию, без луча-прицела: игрок подходит — у объекта
/// появляется плашка с подсказкой, по E вызывается IInteractable на этом же объекте.
/// Тот же приём, что у входа в магазин (SceneDoorTrigger), но обобщённый:
/// работает для колонок, стоек и любых «подойди и нажми» объектов.
/// </summary>
[DisallowMultipleComponent]
public class ProximityInteractable : MonoBehaviour
{
    [Header("Text")]
    [Tooltip("Пусто — подсказка берётся у IInteractable на этом объекте.")]
    [SerializeField] private string promptText = "";
    [SerializeField] private float plateWorldOffsetY = 1.9f;

    [Header("Proximity")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float activateDistance = 2.2f;
    [SerializeField] private float maxVerticalDifference = 3f;

    private Transform playerTransform;
    private Camera viewCamera;
    private IInteractable interactable;
    private bool playerInRange;
    private Texture2D plateBackground;

    public bool PlayerInRange => playerInRange;

    /// <summary>Настройка из кода: радиус срабатывания (ServiceDirector строит колонки в рантайме).</summary>
    public void Setup(float distance)
    {
        activateDistance = distance;
    }

    private void Awake()
    {
        plateBackground = MakeSolidTexture(new Color(0.02f, 0.03f, 0.04f, 0.82f));
        interactable = ResolveInteractable();
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

        if (playerTransform == null)
        {
            playerInRange = false;
            return;
        }

        Vector3 delta = playerTransform.position - transform.position;
        float horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
        playerInRange = horizontalDistance <= activateDistance &&
                        Mathf.Abs(delta.y) <= maxVerticalDifference;

        if (!playerInRange || !InteractPressed())
        {
            return;
        }

        if (PauseMenu.IsPaused || InteractionLock.IsLocked || SceneLoadingScreen.IsLoading)
        {
            return;
        }

        // Объект под прицелом забирает [E] себе — не перебиваем его.
        if (PlayerInteraction.HasFocusedInteractable)
        {
            return;
        }

        if (interactable == null)
        {
            interactable = ResolveInteractable();
        }

        if (interactable != null)
        {
            interactable.Interact();
        }
    }

    private IInteractable ResolveInteractable()
    {
        MonoBehaviour[] components = GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i] != this && components[i] is IInteractable found)
            {
                return found;
            }
        }

        return null;
    }

    private string CurrentPrompt
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(promptText))
            {
                return promptText;
            }

            if (interactable == null)
            {
                interactable = ResolveInteractable();
            }

            return interactable != null ? interactable.GetInteractPrompt() : "";
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

        string prompt = CurrentPrompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return;
        }

        Vector3 worldPoint = transform.position + Vector3.up * plateWorldOffsetY;
        Vector3 screenPoint = viewCamera.WorldToScreenPoint(worldPoint);
        if (screenPoint.z <= 0.1f)
        {
            // Плашка за камерой — рисовать нечего.
            return;
        }

        // Плашка привязана к колонке, поэтому при взгляде в сторону уезжает за край экрана.
        const float offscreenMargin = 80f;
        if (screenPoint.x < -offscreenMargin || screenPoint.x > Screen.width + offscreenMargin ||
            screenPoint.y < -offscreenMargin || screenPoint.y > Screen.height + offscreenMargin)
        {
            return;
        }

        float x = screenPoint.x;
        float y = Screen.height - screenPoint.y;

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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, activateDistance);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * plateWorldOffsetY);
    }
}
