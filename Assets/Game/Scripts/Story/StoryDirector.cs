using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Оркестратор нулевой версии: сам поднимается при запуске игры, создаёт DialogueManager,
/// KotovVisit, NightEvents и ServiceDirector, регистрирует действия диалогов, хранит
/// дровник (до 6 поленьев) и точки мира для сосны/перчатки/силуэта.
/// Сцены не правит: всё, что нужно, создаётся в рантайме и живёт в DontDestroyOnLoad.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryDirector : MonoBehaviour
{
    public const int MaxWoodShed = 6;

    public static StoryDirector Instance { get; private set; }

    public DialogueManager Dialogue { get; private set; }
    public KotovVisit Kotov { get; private set; }
    public NightEvents Night { get; private set; }
    public ServiceDirector Service { get; private set; }

    /// <summary>Дровник: до 6 поленьев (отдельно от лап — там до 3).</summary>
    public int WoodShedCount { get; private set; }

    public event System.Action<int> WoodShedChanged;

    private GameObject root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject holder = new GameObject("SnowpawStory");
        holder.AddComponent<StoryDirector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Dialogue = gameObject.AddComponent<DialogueManager>();
        Kotov = gameObject.AddComponent<KotovVisit>();
        Night = gameObject.AddComponent<NightEvents>();
        Service = gameObject.AddComponent<ServiceDirector>();
        gameObject.AddComponent<Day1WorldBootstrap>();
        gameObject.AddComponent<PlayerColdEffects>();
        RegisterDialogueActions();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ================= ДРОВНИК =================

    /// <summary>Переложить полено в дровник. Возвращает число принятых поленьев.</summary>
    public int AddToWoodShed(int amount)
    {
        int accepted = Mathf.Clamp(amount, 0, MaxWoodShed - WoodShedCount);
        if (accepted <= 0)
        {
            Debug.Log("[Snowpaw Station] Дровник полон (6 поленьев).");
            return 0;
        }

        WoodShedCount += accepted;
        Debug.Log($"[Snowpaw Station] Дровник: {WoodShedCount}/{MaxWoodShed}.");
        WoodShedChanged?.Invoke(WoodShedCount);
        DayLoopManager.Instance?.NotifyMorningTaskDone("woodshed_6", accepted);
        return accepted;
    }

    /// <summary>Взять полено из дровника в лапы (ночью, на растопку).</summary>
    public bool TakeFromWoodShed(int amount = 1)
    {
        if (WoodShedCount < amount)
        {
            return false;
        }

        WoodShedCount -= amount;
        WoodShedChanged?.Invoke(WoodShedCount);
        return true;
    }

    // ================= ПИСЬМА И НАБЛЮДЕНИЯ =================

    private void GiveLetter(int letterNumber)
    {
        if (GameState.HasLetter(letterNumber))
        {
            Debug.LogWarning($"[Snowpaw Station] Письмо №{letterNumber} уже получено — повтор игнорирован.");
            DayLoopManager.Instance?.NotifyEveningCompleted();
            return;
        }

        bool clear = Letters.EvaluateClarity(letterNumber);
        GameState.GiveLetter(letterNumber, clear);
        AddNote($"Письмо №{letterNumber}: {Letters.GetDescription(letterNumber)}");

        if (letterNumber == 2 && !clear)
        {
            AddObservation("Марки на конверте нет.");
        }

        DayLoopManager.Instance?.NotifyEveningCompleted();
    }

    private void AddNote(string text) => GameState.AddNote(text);

    private void AddObservation(string text) => GameState.AddObservation(text);

    // ================= ДЕЙСТВИЯ ДИАЛОГОВ =================

    private void RegisterDialogueActions()
    {
        DialogueActions.Clear();
        DialogueActions.Register("take_letter_1", () => GiveLetter(1));
        DialogueActions.Register("take_letter_2", () => GiveLetter(2));
        DialogueActions.Register("take_letter_3", () => GiveLetter(3));
        DialogueActions.Register("gaze_break", () => Kotov?.BreakGaze());
        DialogueActions.Register("notice_bare_paw", () => GameState.SetNoticedBarePaw(true));
        DialogueActions.Register("kotov_decline", () => AddNote("Котов не заходит: «Нельзя мне. Дом выстудишь»."));
        DialogueActions.Register("danila_legend_answer", () => AddNote("Данила: почтальон приходил в шесть вечера, как по часам. Фамилия на «К»."));
        DialogueActions.Register("pickup_warm_stove", () =>
        {
            AddNote("Водитель красного пикапа погрелся у печи.");
            Service?.MarkPickupWarmed();
        });
        DialogueActions.Register("pickup_pay", () => AddShiftLog("Красный пикап расплатился и уехал."));
        DialogueActions.Register("pickup_leave", () => Service?.MarkPickupDone());
        DialogueActions.Register("night_sleep", () => Night?.FinishNight());
        DialogueActions.Register("night3_look_again", () => Night?.HideSilhouette());
        DialogueActions.Register("night3_go_out", () => Night?.GoOutAtNight3());
        DialogueActions.Register("slice_card_close", () => Night?.ShowSliceEnd());
    }

    private void AddShiftLog(string text) => GameState.AddShiftLog(text);

    // ================= МИР: СОСНА, ПЕРЧАТКА, СИЛУЭТ =================

    /// <summary>Точка старой сосны: ищем в StationMain по имени, иначе — за станцией.</summary>
    public Transform FindOldPine()
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (!t.gameObject.scene.IsValid() || !t.gameObject.activeInHierarchy)
            {
                continue;
            }

            string n = t.name.ToLowerInvariant();
            if ((n.Contains("pine") || n.Contains("сосн") || n.Contains("tree")) && !n.Contains("camera"))
            {
                return t;
            }
        }

        return null;
    }
}
