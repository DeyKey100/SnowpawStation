using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private IInteractable currentInteractable;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
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
        if (mainCamera == null) return;

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

    private void OnDrawGizmosSelected()
    {
        if (mainCamera == null) return;
        Gizmos.color = Color.yellow;
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.DrawRay(ray.origin, ray.direction * interactDistance);
    }
}