using UnityEngine;

/// <summary>
/// Визит Котова (вечер, 18:00). Стук в дверь, диалог, механика взгляда:
/// отвёл взгляд — Котов исчезает, открытка остаётся на пороге (её можно подобрать);
/// удержал взгляд до конца — дополнительная реплика. В письме отказано не бывает.
/// </summary>
[DisallowMultipleComponent]
public sealed class KotovVisit : MonoBehaviour
{
    private const float GazeDotThreshold = 0.55f;
    private const float BreakAfterSeconds = 0.9f;

    public enum VisitState { Waiting, InProgress, Vanished, Done }

    public VisitState State { get; private set; } = VisitState.Waiting;

    /// <summary>Точка Котова (за дверью станции).</summary>
    private Transform kotovAnchor;
    private Camera mainCamera;
    private DialogueManager dialogue;
    private float lookedAwayTimer;
    private bool broken;
    private int visitDay;
    private GameObject doorstepLetter;

    /// <summary>Вечер наступил в интерьере: визит ждёт, пока игрок выйдет на улицу.</summary>
    private int pendingVisitDay;
    private string lastSceneName = string.Empty;

    private void Start()
    {
        dialogue = StoryDirector.Instance != null ? StoryDirector.Instance.Dialogue : null;
        if (dialogue != null)
        {
            dialogue.DialogueFinished += OnAnyDialogueFinished;
        }

        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged += OnPhaseChanged;
            DayLoopManager.Instance.DayStarted += OnDayStarted;
        }
    }

    /// <summary>Новый день — визит снова доступен (иначе после дня 1 State навсегда Done).</summary>
    private void OnDayStarted(int day)
    {
        State = VisitState.Waiting;
        broken = false;
        lookedAwayTimer = 0f;
        pendingVisitDay = 0;
    }

    private void OnAnyDialogueFinished(DialogueScript finished)
    {
        if (finished != null && finished.id != null && finished.id.StartsWith("kotov_day"))
        {
            OnDialogueFinished(finished);
        }
    }

    private void OnDestroy()
    {
        if (DayLoopManager.Instance != null)
        {
            DayLoopManager.Instance.PhaseChanged -= OnPhaseChanged;
            DayLoopManager.Instance.DayStarted -= OnDayStarted;
        }
    }

    private void OnPhaseChanged(int day, DayLoopManager.DayState state)
    {
        if (state != DayLoopManager.DayState.Evening || State != VisitState.Waiting)
        {
            return;
        }

        // Вечер наступает и в магазине: почтальон не может стучать в дверь,
        // пока игрок внутри, поэтому откладываем визит до выхода на улицу.
        pendingVisitDay = day;
        if (IsPlayerOutdoors)
        {
            BeginVisit(day);
        }
    }

    /// <summary>Игрок на улице (не в интерьере заправки).</summary>
    private static bool IsPlayerOutdoors
    {
        get
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string name = scene.IsValid() ? scene.name.ToLowerInvariant() : string.Empty;
            return !name.Contains("interior");
        }
    }

    private void BeginVisit(int day)
    {
        pendingVisitDay = 0;
        visitDay = day;
        State = VisitState.InProgress;
        lookedAwayTimer = 0f;
        broken = false;
        kotovAnchor = ResolveAnchor();
        mainCamera = Camera.main;

        if (dialogue != null)
        {
            dialogue.Show(StoryDialogues.KotovVisit(day));
        }
    }

    /// <summary>Точка Котова: крыльцо станции (дверь), иначе позиция игрока минус вперёд.</summary>
    private Transform ResolveAnchor()
    {
        // Ищем дверь в текущей (активной) сцене, а не в DontDestroyOnLoad.
        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                doorTransform = null;
                FindDoorRecursive(roots[i].transform);
                if (doorTransform != null)
                {
                    return doorTransform;
                }
            }
        }

        return null;
    }

    private Transform doorTransform;

    private void FindDoorRecursive(Transform t)
    {
        if (doorTransform != null)
        {
            return;
        }

        string n = t.name.ToLowerInvariant();
        if (n.Contains("door") && !n.Contains("interior"))
        {
            doorTransform = t;
            return;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            FindDoorRecursive(t.GetChild(i));
            if (doorTransform != null)
            {
                return;
            }
        }
    }

    private void Update()
    {
        TryStartPendingVisit();

        // Следим за взглядом только пока идёт СВОЙ диалог: иначе «отвести взгляд»
        // обрывало чужую реплику (котлета_farewell), а визит при этом терялся.
        if (State != VisitState.InProgress || !IsKotovScriptActive)
        {
            return;
        }

        TrackGaze();
    }

    /// <summary>
    /// Отложенный визит: вечер пришёл, пока игрок был в магазине — стучим,
    /// как только он снова оказался на улице.
    /// </summary>
    private void TryStartPendingVisit()
    {
        if (pendingVisitDay <= 0 || State != VisitState.Waiting)
        {
            return;
        }

        if (!IsPlayerOutdoors)
        {
            return;
        }

        if (OutsideSceneJustLoaded())
        {
            return;
        }

        int day = pendingVisitDay;
        pendingVisitDay = 0;
        BeginVisit(day);
    }

    /// <summary>
    /// Первый кадр после загрузки сцены: игрока ещё не перенесли к точке выхода,
    /// поэтому визит ждём один кадр (иначе Котов ищет дверь в ещё не собранной сцене).
    /// </summary>
    private bool OutsideSceneJustLoaded()
    {
        string current = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (current == lastSceneName)
        {
            return false;
        }

        lastSceneName = current;
        return true;
    }

    private bool IsKotovScriptActive
    {
        get
        {
            if (dialogue == null)
            {
                return false;
            }

            DialogueScript current = dialogue.Current;
            return current != null && current.id != null && current.id.StartsWith("kotov_day");
        }
    }

    private void TrackGaze()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null || kotovAnchor == null)
        {
            // Нет камеры или точки — взгляд не проверяем (без наказания).
            return;
        }

        Vector3 toKotov = (kotovAnchor.position - mainCamera.transform.position).normalized;
        float dot = Vector3.Dot(mainCamera.transform.forward, toKotov);

        if (dot < GazeDotThreshold)
        {
            lookedAwayTimer += Time.deltaTime;
            if (lookedAwayTimer >= BreakAfterSeconds)
            {
                BreakGaze();
            }
        }
        else
        {
            lookedAwayTimer = 0f;
        }
    }

    /// <summary>Взгляд отведён: Котов исчезает, открытка остаётся на пороге.</summary>
    public void BreakGaze()
    {
        if (State != VisitState.InProgress || broken)
        {
            return;
        }

        broken = true;
        State = VisitState.Vanished;

        if (dialogue != null)
        {
            dialogue.Abort();
        }

        Debug.Log("[Snowpaw Station] Котов исчез: взгляд отведён. Открытка осталась на пороге.");
        SpawnDoorstepLetter();
    }

    private void SpawnDoorstepLetter()
    {
        if (doorstepLetter != null)
        {
            return;
        }

        Vector3 pos = kotovAnchor != null ? kotovAnchor.position : Vector3.zero;

        // Отодвигаем от полотна двери: иначе луч прицела упирается в дверь, а не в открытку.
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toPlayer = cam.transform.position - pos;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                pos += toPlayer.normalized * 0.9f;
            }
        }

        GameObject letter = GameObject.CreatePrimitive(PrimitiveType.Cube);
        letter.name = "DoorstepLetter";
        letter.transform.SetParent(null);
        // Точка сцены: x = 0, y = 0, z — от порога.
        letter.transform.position = new Vector3(0f, 0f, pos.z);
        letter.transform.localScale = new Vector3(0.3f, 0.04f, 0.22f);
        letter.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
        letter.layer = LayerMask.NameToLayer("Interactable");

        var pickup = letter.AddComponent<DoorstepLetterInteractable>();
        pickup.Setup(visitDay);
        ProximityInteractable.EnsureOn(letter, 2.4f);
        doorstepLetter = letter;
    }

    /// <summary>Открытка лежит на пороге и ещё не подобрана (дверь в это время молчит).</summary>
    public bool IsLetterWaiting => doorstepLetter != null;

    private void Update_()
    {
        // Заглушка для симметрии; реальная логика в Update/TrackGaze.
    }

    /// <summary>Диалог с Котовым дошёл до конца — взгляд был удержан.</summary>
    public void OnDialogueFinished(DialogueScript finished)
    {
        if (State != VisitState.InProgress)
        {
            return;
        }

        State = VisitState.Done;

        string extra = StoryDialogues.KotovGazeHeldLine(visitDay);
        if (dialogue != null && !string.IsNullOrEmpty(extra))
        {
            DialogueScript tail = DialogueBuilder.Create("kotov_gaze");
            tail.Say("КОТОВ", extra);
            dialogue.Show(tail, OnGazeTailFinished);
        }
        else
        {
            OnGazeTailFinished();
        }
    }

    private void OnGazeTailFinished()
    {
        GameState.SetGazeHeld(visitDay, true);
        Debug.Log($"[Snowpaw Station] Взгляд на Котова удержан (день {visitDay}).");

        // Игрок мог удержать взгляд, не выбрав «Взять открытку»: письмо всё равно должно дойти,
        // иначе вечер не закрывается и день не переключается.
        if (!GameState.HasLetter(visitDay))
        {
            SpawnDoorstepLetter();
            Debug.Log("[Snowpaw Station] Открытка осталась на пороге: подберите её [E].");
        }
    }

    /// <summary>Перчатка/письмо с порога подобрано — вечер закрыт.</summary>
    public void OnDoorstepLetterTaken(int day)
    {
        if (doorstepLetter != null)
        {
            Destroy(doorstepLetter);
            doorstepLetter = null;
        }

        GameState.GiveLetter(day, Letters.EvaluateClarity(day));
        DayLoopManager.Instance?.NotifyEveningCompleted();
    }

    private void OnGUI()
    {
        if (State != VisitState.InProgress || dialogue == null || DialogueManager.IsActive)
        {
            return;
        }

        GUI.Label(new Rect(Screen.width * 0.5f - 110f, Screen.height - 90f, 220f, 30f),
            "*Три тихих стука в дверь*", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                normal = { textColor = new Color(0.9f, 0.9f, 1f) }
            });
    }
}
