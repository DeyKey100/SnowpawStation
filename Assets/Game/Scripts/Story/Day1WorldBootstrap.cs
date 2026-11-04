using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Рантайм-объекты дня 1: записка под кружкой (интерьер) и дровник (двор StationMain).
/// Сцены не правятся: объекты создаются при загрузке сцены и живут в ней.
/// </summary>
[DisallowMultipleComponent]
public sealed class Day1WorldBootstrap : MonoBehaviour
{
    private GameObject note;
    private GameObject shed;

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        string name = scene.name.ToLowerInvariant();
        bool interior = name.Contains("interior");

        // Записка нужна в любой сцене: без неё не закрывается утро дня 1.
        SpawnNote(scene, interior);

        if (!interior && DayLoopManager.Instance != null && DayLoopManager.Instance.CurrentDay >= 3)
        {
            SpawnShed(scene);
        }
    }

    private void SpawnNote(Scene scene, bool interior)
    {
        // Записку читают один раз за прохождение: после перезагрузки сцены (магазин → улица)
        // она не должна появляться снова.
        if (GameState.IsDayEventDone(KeeperNoteInteractable.ReadEventId))
        {
            return;
        }

        if (FindInScene(scene, "keepernote") != null)
        {
            return;
        }

        Transform stove = interior ? null : FindInScene(scene, "stove");
        Transform door = FindInScene(scene, "door");

        Vector3 pos;
        if (interior)
        {
            pos = new Vector3(-0.6f, 1.12f, 0.4f); // у стойки, «под кружкой»
        }
        else if (door != null)
        {
            // Перед дверью, но не на пороге: иначе плашка записки перебивает «Войти»
            // (взаимодействия теперь по расстоянию, а не по прицелу).
            Transform player = FindInScene(scene, "playercapsule");
            Vector3 dir = player != null ? player.position - door.position : Vector3.back;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            pos = door.position + dir * 2.8f;
            // Снежный пол — куб с верхней гранью на y≈0.5: ниже записка утонула бы в нём.
            pos.y = door.position.y + 0.06f;
        }
        else if (stove != null)
        {
            pos = new Vector3(stove.position.x + 0.75f, 0.35f, stove.position.z + 0.55f);
        }
        else
        {
            pos = new Vector3(-1.3f, 0.35f, -7.4f);
        }

        note = GameObject.CreatePrimitive(PrimitiveType.Cube);
        note.name = "KeeperNote";
        note.transform.position = pos;
        note.transform.localScale = new Vector3(0.34f, 0.03f, 0.46f);
        note.transform.rotation = Quaternion.Euler(0f, interior ? 0f : 35f, 0f);
        ApplyInteractableLayer(note);

        Renderer renderer = note.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.color = new Color(0.95f, 0.9f, 0.72f);
            renderer.sharedMaterial = mat;
        }

        note.AddComponent<KeeperNoteInteractable>();
        ProximityInteractable.EnsureOn(note);
        Debug.Log($"[Snowpaw Station] Записка смотрителя: {pos}.");
    }

    private void SpawnShed(Scene scene)
    {
        if (shed != null || FindInScene(scene, "woodshed") != null)
        {
            return;
        }

        Transform woodPile = FindInScene(scene, "wood");
        shed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shed.name = "WoodShed";
        Vector3 pos = woodPile != null
            ? woodPile.position + woodPile.right * 2.5f
            : new Vector3(8f, 0.6f, 3f);
        shed.transform.position = new Vector3(pos.x, 0.6f, pos.z);
        shed.transform.localScale = new Vector3(1.4f, 1.2f, 0.8f);
        ApplyInteractableLayer(shed);

        Renderer renderer = shed.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.28f, 0.19f, 0.13f);
            renderer.sharedMaterial = mat;
        }

        shed.AddComponent<WoodShedInteractable>();
        ProximityInteractable.EnsureOn(shed, 2.4f);
        Debug.Log("[Snowpaw Station] Дровник создан у колоды (дровник до 6 поленьев).");
    }

    private static Transform FindInScene(Scene scene, string namePart)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindRecursive(roots[i].transform, namePart);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindRecursive(Transform t, string namePart)
    {
        if (t.name.ToLowerInvariant().Contains(namePart))
        {
            return t;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindRecursive(t.GetChild(i), namePart);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void ApplyInteractableLayer(GameObject go)
    {
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0)
        {
            go.layer = layer;
        }
    }
}

/// <summary>
/// Мини-игра колки: бегунок бегает, зелёная зона в центре. ЛКМ в зоне = полено.
/// Промах — бегунок просто перезапускается. На время игры вешается InteractionLock "chopping".
/// </summary>
[DisallowMultipleComponent]
public sealed class WoodChoppingMinigame : MonoBehaviour
{
    private float marker;       // 0..1
    private float direction = 1f;
    private float speed = 1.4f;
    private bool active;
    private bool success;

    public bool IsActive => active;

    /// <summary>Запуск. onSuccess — полено добыто.</summary>
    public void Begin(System.Action onSuccess)
    {
        if (active)
        {
            return;
        }

        active = true;
        success = false;
        marker = 0f;
        direction = 1f;
        speed = 1.4f;
        InteractionLock.Add("chopping");
        StartCoroutine(Run(onSuccess));
 }

    private System.Collections.IEnumerator Run(System.Action onSuccess)
    {
        while (active)
        {
            yield return null;
        }

        InteractionLock.Remove("chopping");
        if (success)
        {
            onSuccess?.Invoke();
        }
    }

    private void Update()
    {
        if (!active)
        {
            return;
        }

        marker += direction * speed * Time.deltaTime;
        if (marker >= 1f)
        {
            marker = 1f;
            direction = -1f;
            speed += 0.1f;
        }
        else if (marker <= 0f)
        {
            marker = 0f;
            direction = 1f;
            speed += 0.1f;
        }

        bool pressed;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
        pressed = Input.GetMouseButtonDown(0);
#endif

        if (pressed)
        {
            bool hit = marker >= 0.4f && marker <= 0.6f;
            if (hit)
            {
                success = true;
                active = false;
            }
            else
            {
                // Промах: бегунок продолжает бег (новый круг ускоряется).
                marker = 0f;
                direction = 1f;
            }
        }

        // Выход из мини-игры — Q (Tab занят записной книжкой, Space теперь просто ничего не делает).
        bool quit;
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        quit = kb != null && kb.qKey.wasPressedThisFrame;
#else
        quit = Input.GetKeyDown(KeyCode.Q);
#endif
        if (quit)
        {
            active = false;
        }
    }

    private void OnGUI()
    {
        if (!active)
        {
            return;
        }

        float w = 420f, h = 26f;
        Rect bar = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f, w, h);
        GUI.Box(bar, GUIContent.none);
        Rect green = new Rect(bar.x + bar.width * 0.4f, bar.y, bar.width * 0.2f, h);
        GUI.color = new Color(0.3f, 0.8f, 0.35f, 0.85f);
        GUI.DrawTexture(green, Texture2D.whiteTexture);
        GUI.color = Color.white;
        Rect knob = new Rect(bar.x + bar.width * marker - 3f, bar.y - 4f, 6f, h + 8f);
        GUI.DrawTexture(knob, Texture2D.whiteTexture);
        GUI.Label(new Rect(bar.x, bar.y + h + 8f, w, 26f),
            "ЛКМ — рубить в зелёной зоне (Q — выйти)", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            });
    }
}
