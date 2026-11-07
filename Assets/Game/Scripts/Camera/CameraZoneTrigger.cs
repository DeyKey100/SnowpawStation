using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class CameraZoneTrigger : MonoBehaviour
{
    [SerializeField] private CinemachineCamera zoneCamera;
    [SerializeField] private CinemachineCamera defaultCamera;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;
    [SerializeField] private bool restoreOnExit = true;

    private Collider triggerCollider;
    private int defaultCameraPriority;
    private int zoneCameraPriority;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        triggerCollider.isTrigger = true;

        if (zoneCamera == null)
        {
            zoneCamera = FindCamera("DialogueCamera");
        }

        if (defaultCamera == null)
        {
            CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (CinemachineCamera camera in cameras)
            {
                if (camera != zoneCamera)
                {
                    defaultCamera = camera;
                    break;
                }
            }
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
        zoneCameraPriority = zoneCamera.Priority.Value;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!enabled || !other.CompareTag(playerTag))
        {
            return;
        }

        defaultCamera.Priority = new PrioritySettings { Value = inactivePriority };
        zoneCamera.Priority = new PrioritySettings { Value = activePriority };
    }

    private void OnTriggerExit(Collider other)
    {
        if (!enabled || !restoreOnExit || !other.CompareTag(playerTag))
        {
            return;
        }

        defaultCamera.Priority = new PrioritySettings { Value = defaultCameraPriority };
        zoneCamera.Priority = new PrioritySettings { Value = zoneCameraPriority };
    }

    private static CinemachineCamera FindCamera(string cameraName)
    {
        GameObject cameraObject = GameObject.Find(cameraName);
        return cameraObject == null ? null : cameraObject.GetComponent<CinemachineCamera>();
    }
}
