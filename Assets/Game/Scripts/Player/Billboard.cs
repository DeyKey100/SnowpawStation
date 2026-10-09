using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Start()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (mainCameraTransform == null) return;

        // Рассчитываем направление от объекта к камере
        Vector3 direction = mainCameraTransform.position - transform.position;
        
        // Игнорируем разницу по высоте (ось Y) для предотвращения наклона спрайта
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}