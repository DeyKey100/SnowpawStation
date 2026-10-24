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

    private void Awake()
    {
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

        Camera[] cameras = FindObjectsOfType<Camera>();
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private void OnGUI()
    {
        if (currentInteractable == null) return;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            normal = { textColor = new Color(0.08f, 0.08f, 0.08f, 1f) }
        };

        GUI.Label(new Rect(0f, Screen.height - 90f, Screen.width, 40f),
            currentInteractable.GetInteractPrompt(), style);
    }

    private void OnDrawGizmosSelected()
    {
        if (interactionCamera == null) return;
        Gizmos.color = Color.yellow;
        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawRay(ray.origin, ray.direction * interactDistance);
    }
}