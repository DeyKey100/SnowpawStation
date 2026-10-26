using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private Camera interactionCamera;

    private IInteractable currentInteractable;
    private bool cameraErrorReported;
    private Texture2D promptBackground;

    private void OnDestroy()
    {
        if (promptBackground != null)
        {
            Destroy(promptBackground);
        }
    }

    private void Awake()
    {
        promptBackground = MakeSolidTexture(new Color(0.02f, 0.03f, 0.04f, 0.82f));

        if (interactionCamera == null)
        {
            interactionCamera = FindMainCamera();
        }

        if (interactionCamera == null)
        {
            Debug.LogError("PlayerInteraction не нашёл камеру. Назначьте Main Camera в поле Interaction Camera.", this);
            cameraErrorReported = true;
        }
    }

    private void Update()
    {
        DetectInteractable();

        if (currentInteractable != null && IsInteractPressed())
        {
            currentInteractable.Interact();
        }
    }

    private void DetectInteractable()
    {
        if (interactionCamera == null)
        {
            if (!cameraErrorReported)
            {
                Debug.LogError("PlayerInteraction не может определить объект: камера недоступна.", this);
                cameraErrorReported = true;
            }

            currentInteractable = null;
            return;
        }

        // Луч пускается из центра экрана (прицела)
        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                currentInteractable = interactable;
                return;
            }
        }

        currentInteractable = null;
    }

    private bool IsInteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return interactKey == KeyCode.E &&
               Keyboard.current != null &&
               Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(interactKey);
#endif
    }

    private Camera FindMainCamera()
    {
        Camera camera = Camera.main;
        if (camera != null) return camera;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private void OnGUI()
    {
        if (currentInteractable == null) return;

        string prompt = currentInteractable.GetInteractPrompt();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            normal =
            {
                background = promptBackground,
                textColor = Color.white
            }
        };

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(18, 18, 0, 0),
            normal = { textColor = Color.white }
        };

        Vector2 textSize = style.CalcSize(new GUIContent(prompt));
        float promptWidth = Mathf.Clamp(textSize.x + 36f, 260f, Screen.width - 32f);
        Rect promptRect = new Rect(
            Screen.width * 0.5f - promptWidth * 0.5f,
            Screen.height - 105f,
            promptWidth,
            52f);

        GUI.Box(promptRect, GUIContent.none, boxStyle);
        GUI.Label(promptRect, prompt, style);
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
        if (interactionCamera == null) return;
        Gizmos.color = Color.yellow;
        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawRay(ray.origin, ray.direction * interactDistance);
    }
}