using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private IInteractable currentInteractable;
    private Camera mainCamera;
    private bool cameraErrorReported;

    private void Awake()
    {
        mainCamera = FindMainCamera();

        if (mainCamera == null)
        {
            GameObject cameraObject = new GameObject("PlayerFallbackCamera");
            cameraObject.transform.SetParent(transform);
            cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;
            mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            Debug.LogError("PlayerInteraction не нашёл Main Camera. Создана временная камера на игроке.", this);
            cameraErrorReported = true;
        }
    }

    private void Update()
    {
        DetectInteractable();

        if (currentInteractable != null && Input.GetKeyDown(interactKey))
        {
            currentInteractable.Interact();
        }
    }

    private void DetectInteractable()
    {
        if (mainCamera == null)
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
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
        {
            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
            {
                currentInteractable = interactable;
                return;
            }
        }

        currentInteractable = null;
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
            fontSize = 18
        };

        GUI.Label(new Rect(0f, Screen.height - 90f, Screen.width, 40f),
            currentInteractable.GetInteractPrompt(), style);
    }

    private void OnDrawGizmosSelected()
    {
        if (mainCamera == null) return;
        Gizmos.color = Color.yellow;
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawRay(ray.origin, ray.direction * interactDistance);
    }
}