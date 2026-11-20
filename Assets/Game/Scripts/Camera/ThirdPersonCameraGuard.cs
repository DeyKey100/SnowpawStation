using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Присмотр за видимостью частей персонажа.
///
/// Первая задача — служебный меш капсулы. В префабе у него есть MeshRenderer
/// рядом с CapsuleCollider, и он выключен только флагом компонента. Любой код,
/// который перебирает рендереры персонажа и включает их «как было», зажигает
/// капсулу обратно: игрок видит серый цилиндр вокруг себя. Поэтому капсула
/// гасится адресно и подстраховывается каждый кадр.
///
/// Вторая задача (необязательная) — спрятать модель, когда камера третьего лица
/// подъехала вплотную и голова персонажа закрывает кадр. По умолчанию выключена:
/// модель должна быть видна.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class ThirdPersonCameraGuard : MonoBehaviour
{
    private const string CameraRootName = "PlayerCameraRoot";
    private const string CapsuleObjectName = "Capsule";
    private const string ModelObjectName = "CharacterModel";

    [SerializeField, Tooltip("Гасить меш капсулы-коллайдера: это служебная геометрия, игрок не должен её видеть.")]
    private bool hideCapsule = true;

    [SerializeField, Tooltip("Прятать модель персонажа, когда камера подъехала вплотную. По умолчанию выключено.")]
    private bool hideModelWhenCameraClose = false;

    [SerializeField, Tooltip("Ближе этого расстояния модель персонажа скрывается.")]
    private float hideDistance = 1.7f;

    [SerializeField, Tooltip("С какого расстояния модель снова показывается (гистерезис, чтобы не мерцала).")]
    private float showDistance = 2.1f;

    private Transform cameraRoot;
    private Camera mainCamera;
    private Renderer[] capsuleRenderers;
    private Renderer[] modelRenderers;
    private bool modelHidden;

    /// <summary>Прятать модель, когда камера подъехала вплотную.</summary>
    public bool HideModelWhenCameraClose
    {
        get => hideModelWhenCameraClose;
        set
        {
            hideModelWhenCameraClose = value;
            if (!value && modelHidden)
            {
                modelHidden = false;
                ApplyModel(true);
            }
        }
    }

    /// <summary>Навешивает присмотр на корень персонажа, если его там ещё нет.</summary>
    public static ThirdPersonCameraGuard EnsureOn(GameObject player, bool hideCapsuleAlways = true)
    {
        if (player == null)
        {
            return null;
        }

        ThirdPersonCameraGuard guard = player.GetComponent<ThirdPersonCameraGuard>();
        if (guard == null)
        {
            guard = player.AddComponent<ThirdPersonCameraGuard>();
        }

        guard.hideCapsule = hideCapsuleAlways;
        guard.modelHidden = false;
        guard.CacheRefs();
        guard.ApplyCapsule();
        guard.ApplyModel(true);

        return guard;
    }

    private void Awake()
    {
        CacheRefs();
    }

    private void OnEnable()
    {
        modelHidden = false;
        CacheRefs();
        ApplyCapsule();
        ApplyModel(true);
    }

    private void OnDisable()
    {
        // Возвращаем всё как было, чтобы скрипт не оставлял персонажа без капсулы
        // и без модели, если его снимут.
        if (!hideCapsule)
        {
            SetCapsuleVisible(true);
        }

        ApplyModel(true);
        modelHidden = false;
    }

    private void LateUpdate()
    {
        ApplyCapsule();

        if (!hideModelWhenCameraClose)
        {
            if (modelHidden)
            {
                modelHidden = false;
                ApplyModel(true);
            }

            return;
        }

        if (cameraRoot == null || mainCamera == null)
        {
            CacheRefs();
            if (cameraRoot == null || mainCamera == null)
            {
                return;
            }
        }

        float distance = Vector3.Distance(cameraRoot.position, mainCamera.transform.position);
        if (!modelHidden && distance < hideDistance)
        {
            modelHidden = true;
            ApplyModel(false);
        }
        else if (modelHidden && distance > showDistance)
        {
            modelHidden = false;
            ApplyModel(true);
        }
    }

    private void CacheRefs()
    {
        cameraRoot = FindCameraRoot();
        mainCamera = Camera.main;
        capsuleRenderers = FindRenderers(CapsuleObjectName);
        modelRenderers = FindRenderers(ModelObjectName);
    }

    /// <summary>
    /// Перебираем рендереры ТОЛЬКО внутри нужного узла. Раньше здесь стоял
    /// GetComponentsInChildren по всему персонажу — именно он и включал капсулу.
    /// </summary>
    private Renderer[] FindRenderers(string objectName)
    {
        Transform target = FindChild(transform, objectName);
        return target == null ? null : target.GetComponentsInChildren<Renderer>(true);
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Точка обзора ищется по имени в текущей сцене: персонаж бывает как
    /// PlayerCharacter (интерьер), так и PlayerCapsule (улица).
    /// </summary>
    private static Transform FindCameraRoot()
    {
        GameObject root = GameObject.Find(CameraRootName);
        if (root != null)
        {
            return root.transform;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return null;
        }

        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            foreach (Transform child in candidate.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == CameraRootName)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private void ApplyCapsule()
    {
        SetCapsuleVisible(!hideCapsule);
    }

    private void SetCapsuleVisible(bool visible)
    {
        if (capsuleRenderers == null)
        {
            return;
        }

        for (int i = 0; i < capsuleRenderers.Length; i++)
        {
            Renderer renderer = capsuleRenderers[i];
            if (renderer != null && renderer.enabled != visible)
            {
                renderer.enabled = visible;
            }
        }
    }

    private void ApplyModel(bool visible)
    {
        if (modelRenderers == null)
        {
            return;
        }

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            Renderer renderer = modelRenderers[i];
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }
}
