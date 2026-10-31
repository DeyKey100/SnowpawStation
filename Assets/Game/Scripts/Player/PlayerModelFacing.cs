using UnityEngine;

/// <summary>
/// Держит модель игрока в мировом повороте независимо от поворота корня игрока за камерой.
/// Модель разворачивается только когда персонаж реально движется — по направлению движения.
/// </summary>
[DefaultExecutionOrder(1000)]
public class PlayerModelFacing : MonoBehaviour
{
    [SerializeField] private float turnSmoothTime = 0.12f;
    [SerializeField] private float moveThreshold = 0.1f;

    private CharacterController controller;
    private float yaw;
    private float yawVelocity;

    private void Awake()
    {
        controller = GetComponentInParent<CharacterController>();
        yaw = transform.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (controller != null)
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;

            if (velocity.sqrMagnitude > moveThreshold * moveThreshold)
            {
                float targetYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, turnSmoothTime);
            }
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
