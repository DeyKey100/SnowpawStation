using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string openAnimationName = "open";
    [SerializeField] private Animation doorAnimation;
    [SerializeField] private bool ignorePlayerCollisionWhenOpening = true;
    [SerializeField] private string playerTag = "Player";

    private bool animationErrorReported;
    private Collider[] ignoredDoorColliders;
    private Collider[] ignoredPlayerColliders;
    private bool ignoringPlayerCollision;
    private float collisionIgnoreUntil;

    public string GetInteractPrompt()
    {
        return "[E] Открыть дверь";
    }

    public void Interact()
    {
        if (doorAnimation == null)
        {
            doorAnimation = GetComponentInChildren<Animation>();
        }

        if (doorAnimation == null)
        {
            if (!animationErrorReported)
            {
                Debug.LogError(
                    "DoorInteractable не нашёл Animation. Используйте официальный PF_door_shop.prefab из PSX Gas Station Kit.",
                    this);
                animationErrorReported = true;
            }

            return;
        }

        if (!doorAnimation.GetClip(openAnimationName))
        {
            Debug.LogError(
                $"В Animation двери нет клипа '{openAnimationName}'. Проверьте импорт door_shop.fbx.",
                this);
            return;
        }

        if (doorAnimation.isPlaying)
        {
            return;
        }

        if (ignorePlayerCollisionWhenOpening)
        {
            BeginIgnoringPlayerCollision();
        }

        doorAnimation.Play(openAnimationName);
        collisionIgnoreUntil = Time.time + doorAnimation.GetClip(openAnimationName).length + 0.1f;
    }

    private void Update()
    {
        if (ignoringPlayerCollision &&
            (!doorAnimation.isPlaying || Time.time >= collisionIgnoreUntil))
        {
            RestorePlayerCollision();
        }
    }

    private void BeginIgnoringPlayerCollision()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        ignoredDoorColliders = GetComponentsInChildren<Collider>(true);
        ignoredPlayerColliders = player != null
            ? player.GetComponentsInChildren<Collider>(true)
            : new Collider[0];

        for (int i = 0; i < ignoredDoorColliders.Length; i++)
        {
            if (!ignoredDoorColliders[i].enabled)
            {
                continue;
            }

            for (int j = 0; j < ignoredPlayerColliders.Length; j++)
            {
                if (ignoredPlayerColliders[j].enabled)
                {
                    Physics.IgnoreCollision(ignoredDoorColliders[i], ignoredPlayerColliders[j], true);
                }
            }
        }

        ignoringPlayerCollision = true;
    }

    private void RestorePlayerCollision()
    {
        for (int i = 0; i < ignoredDoorColliders.Length; i++)
        {
            if (ignoredDoorColliders[i] == null)
            {
                continue;
            }

            for (int j = 0; j < ignoredPlayerColliders.Length; j++)
            {
                if (ignoredPlayerColliders[j] != null)
                {
                    Physics.IgnoreCollision(ignoredDoorColliders[i], ignoredPlayerColliders[j], false);
                }
            }
        }

        ignoredDoorColliders = null;
        ignoredPlayerColliders = null;
        ignoringPlayerCollision = false;
    }
}
