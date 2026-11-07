using Unity.Cinemachine;
using UnityEngine;

public sealed class CameraZoneTrigger : MonoBehaviour
{
    [Header("Runtime camera")]
    [SerializeField] private GameObject zoneCameraPrefab;
    [SerializeField] private CinemachineVirtualCamera zoneCamera;
    [SerializeField] private CinemachineVirtualCamera defaultCamera;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;
    [SerializeField] private int fallbackDefaultPriority = 10;
    [SerializeField] private bool restoreOnExit = true;
    [SerializeField] private bool activateFromTrigger;
    [SerializeField] private string playerTag = "Player";

    private Collider triggerCollider;
    private int defaultCameraPriority;
    private int zoneCameraPriority;
    private bool dialogueSubscribed;
    private bool triggerActive;
    private bool stateActive = true;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (GetComponent<Camera>() == null && zoneCamera == null)
        {
            enabled = false;
            return;
        }

        if (defaultCamera == null)
        {
            defaultCamera = FindDefaultCamera();
        }

        if (zoneCamera == null)
        {
            zoneCamera = FindZoneCamera();
        }

        if (defaultCamera == zoneCamera)
        {
            defaultCamera = FindDefaultCamera(zoneCamera);
        }

        if (zoneCamera == null && zoneCameraPrefab != null)
        {
            GameObject cameraObject = Instantiate(zoneCameraPrefab);
            cameraObject.name = zoneCameraPrefab.name;
            zoneCamera = cameraObject.GetComponent<CinemachineVirtualCamera>();
        }

        if (zoneCamera == null || defaultCamera == null)
        {
            Debug.LogError($"{nameof(CameraZoneTrigger)} on {name} requires both camera references.", this);
            enabled = false;
            return;
        }

        zoneCamera.Follow = defaultCamera.Follow;
        zoneCamera.LookAt = defaultCamera.LookAt;
        defaultCameraPriority = defaultCamera.Priority.Value;
        if (defaultCameraPriority <= inactivePriority)
        {
            defaultCameraPriority = fallbackDefaultPriority;
        }

        zoneCameraPriority = zoneCamera.Priority.Value;
        SetCameraActive(false);
    }

    private void OnEnable()
    {
        PlayerControlLock.LockStateChanged += OnControlLockChanged;
    }

    private void Update()
    {
        RefreshState();

        if (!dialogueSubscribed && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.DialogueStarted += OnDialogueStarted;
            DialogueManager.Instance.DialogueFinished += OnDialogueFinished;
            dialogueSubscribed = true;
            RefreshState();
        }
    }

    private void OnDisable()
    {
        PlayerControlLock.LockStateChanged -= OnControlLockChanged;
        UnsubscribeDialogue();
    }

    private void OnDestroy()
    {
        UnsubscribeDialogue();
    }

    private void OnDialogueStarted(DialogueScript script)
    {
        RefreshState();
    }

    private void OnDialogueFinished(DialogueScript script)
    {
        RefreshState();
    }

    private void OnControlLockChanged(bool locked)
    {
        RefreshState();
    }

    private void RefreshState()
    {
        bool active = (DialogueManager.IsActive || PlayerControlLock.IsLocked) ||
                      (activateFromTrigger && triggerActive);
        SetCameraActive(active);
    }

    private void SetCameraActive(bool active)
    {
        if (zoneCamera == null || defaultCamera == null || stateActive == active)
        {
            return;
        }

        stateActive = active;
        defaultCamera.Priority = new PrioritySettings
        {
            Value = active ? inactivePriority : defaultCameraPriority
        };
        zoneCamera.Priority = new PrioritySettings
        {
            Value = active ? activePriority : zoneCameraPriority
        };
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || !other.CompareTag(playerTag))
        {
            return;
        }

        triggerActive = true;
        RefreshState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || !restoreOnExit || !other.CompareTag(playerTag))
        {
            return;
        }

        triggerActive = false;
        RefreshState();
    }

    private void UnsubscribeDialogue()
    {
        if (!dialogueSubscribed || DialogueManager.Instance == null)
        {
            dialogueSubscribed = false;
            return;
        }

        DialogueManager.Instance.DialogueStarted -= OnDialogueStarted;
        DialogueManager.Instance.DialogueFinished -= OnDialogueFinished;
        dialogueSubscribed = false;
    }

    private static CinemachineVirtualCamera FindDefaultCamera(
        CinemachineVirtualCamera excluded = null)
    {
        CinemachineVirtualCamera[] cameras = FindObjectsByType<CinemachineVirtualCamera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        CinemachineVirtualCamera result = null;
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineVirtualCamera candidate = cameras[i];
            if (candidate == null || candidate == excluded ||
                candidate.GetComponent<CameraZoneTrigger>() != null ||
                candidate.name.Contains("Dialogue"))
            {
                continue;
            }

            if (result == null || candidate.Priority.Value > result.Priority.Value)
            {
                result = candidate;
            }
        }

        return result;
    }

    private static CinemachineVirtualCamera FindZoneCamera()
    {
        CinemachineVirtualCamera[] cameras = FindObjectsByType<CinemachineVirtualCamera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineVirtualCamera candidate = cameras[i];
            if (candidate != null && candidate.name.Contains("Dialogue"))
            {
                return candidate;
            }
        }

        return null;
    }
}
