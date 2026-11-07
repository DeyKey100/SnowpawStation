using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Машина фаз нулевой версии: Утро → Смена → Вечер (18:00, визит Котова) → Ночь → следующий день.
/// Переходы строго условные (задачи, квота машин, сюжетные гости, письмо, ночное событие).
/// Никаких таймеров и задержек, «утро завершается через N секунд» здесь нет.
/// День 1-3; после ночи дня 3 — событие SliceCompleted (конец нулевой версии).
/// </summary>
[DisallowMultipleComponent]
public class DayLoopManager : MonoBehaviour
{
    public enum DayState { Morning, WorkShift, Evening, NightTransition }

    public static DayLoopManager Instance { get; private set; }

    [SerializeField] private int currentDay = 1;
    [SerializeField] private bool stoveFueledToday;

    public int CurrentDay => currentDay;
    public DayState CurrentState { get; private set; } = DayState.Morning;

    /// <summary>Квота обычных машин на день (нулевая версия: 2).</summary>
    public int RegularCarQuota => DayPlans.GetRegularCarQuota(currentDay);

    /// <summary>Обслужено обычных машин за смену.</summary>
    public int CarsServed { get; private set; }

    /// <summary>Сюжетные гости, которых ещё нужно обслужить до вечера.</summary>
    private readonly List<string> pendingGuests = new List<string>();
    private readonly Dictionary<string, int> taskProgress = new Dictionary<string, int>();

    public event Action<int, DayState> PhaseChanged;
    public event Action<int> DayChanged;
    public event Action<int> DayStarted;
    public event Action<int> SliceCompleted;

    private bool sliceCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Сцена перезагружена: живёт персистентная копия GameSystems, свежий дубль удаляем.
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // Совместимо с сохранённой сценой: если Instance занят не нами — ничего не трогаем.
        if (Instance == this && DayPlans.IsValidDay(currentDay))
        {
            BeginDay(currentDay);
        }
    }

    // --- УТРО: задачи, их прогресс и переход в смену ---

    /// <summary>Все обязательные утренние задачи дня выполнены (совместимо со старым API).</summary>
    public bool MorningTaskCompleted => IsMorningDone(currentDay);

    public bool IsMorningDone(int day)
    {
        List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(day);
        for (int i = 0; i < tasks.Count; i++)
        {
            if (!tasks[i].required)
            {
                continue;
            }

            if (GetTaskProgress(tasks[i].id) < tasks[i].target)
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<DayPlan.MorningTask> GetCurrentMorningTasks()
    {
        return DayPlans.GetMorningTasks(currentDay);
    }

    public int GetTaskProgress(string taskId)
    {
        return taskProgress.TryGetValue(taskId, out int value) ? value : 0;
    }

    /// <summary>
    /// Открыта ли утренняя задача. Записка — задача-ключ: пока её не прочли, дрова,
    /// печь и сугробы закрыты, иначе утро закрывается вслепую.
    /// </summary>
    public bool IsMorningTaskUnlocked(string taskId)
    {
        if (CurrentState != DayState.Morning)
        {
            return true;
        }

        List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(currentDay);
        for (int i = 0; i < tasks.Count; i++)
        {
            if (!tasks[i].gate)
            {
                continue;
            }

            if (tasks[i].id == taskId)
            {
                return true;
            }

            if (GetTaskProgress(tasks[i].id) < tasks[i].target)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Первая незакрытая задача утра — что игроку делать прямо сейчас.</summary>
    public DayPlan.MorningTask GetCurrentTask()
    {
        List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(currentDay);
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].required && GetTaskProgress(tasks[i].id) < tasks[i].target)
            {
                return tasks[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Отметить прогресс обязательной утренней задачи (полено, сугроб, дровник...).
    /// Когда все обязательные задачи закрыты — автоматический переход в Смену. Повторные вызовы
    /// сверх целевого значения просто игнорируются.
    /// </summary>
    public void NotifyMorningTaskDone(string taskId, int amount = 1)
    {
        if (CurrentState != DayState.Morning)
        {
            return;
        }

        DayPlan.MorningTask task = FindTask(taskId);
        if (task == null)
        {
            Debug.LogWarning($"DayLoopManager: задача '{taskId}' не входит в утро дня {currentDay}.");
            return;
        }

        if (!IsMorningTaskUnlocked(taskId))
        {
            Debug.Log($"[Snowpaw Station] '{task.label}' ещё закрыта: сначала прочитайте записку.");
            return;
        }

        int current = GetTaskProgress(taskId);
        int target = Mathf.Max(1, task.target);
        if (current >= target)
        {
            return;
        }

        taskProgress[taskId] = Mathf.Min(target, current + Mathf.Max(1, amount));
        Debug.Log($"[Snowpaw Station] Утро дня {currentDay}: '{task.label}' — {taskProgress[taskId]}/{target}.");

        if (IsMorningDone(currentDay))
        {
            AdvanceTo(DayState.WorkShift, "все утренние задачи выполнены");
        }
    }

    /// <summary>Совместимо со старым API (SnowPileInteractable): закрыть первую незакрытую задачу.</summary>
    public void CompleteMorningTask()
    {
        List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(currentDay);
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].required && GetTaskProgress(tasks[i].id) < tasks[i].target)
            {
                NotifyMorningTaskDone(tasks[i].id, tasks[i].target);
                return;
            }
        }
    }

    // --- СМЕНА: квота обычных машин и сюжетные гости ---

    public void RegisterDayGuests(List<string> guestIds)
    {
        pendingGuests.Clear();
        if (guestIds == null)
        {
            return;
        }

        for (int i = 0; i < guestIds.Count; i++)
        {
            if (!string.IsNullOrEmpty(guestIds[i]) && !pendingGuests.Contains(guestIds[i]))
            {
                pendingGuests.Add(guestIds[i]);
            }
        }
    }

    public bool AreAllGuestsServed => pendingGuests.Count == 0;

    /// <summary>Обслужена обычная машина. Вечер (18:00) наступает после квоты и всех сюжетных гостей.</summary>
    public void NotifyRegularCarServed()
    {
        if (CurrentState != DayState.WorkShift)
        {
            return;
        }

        CarsServed++;
        Debug.Log($"[Snowpaw Station] Обычные машины: {CarsServed}/{RegularCarQuota}.");
        TryAdvanceToEvening();
    }

    /// <summary>Обслужен сюжетный гость (Котлета, курьер, Данила, красный пикап).</summary>
    public void NotifyGuestServed(string guestId)
    {
        if (CurrentState != DayState.WorkShift)
        {
            return;
        }

        if (pendingGuests.Remove(guestId))
        {
            Debug.Log($"[Snowpaw Station] Сюжетный гость '{guestId}' обслужен. Осталось: {pendingGuests.Count}.");
        }

        TryAdvanceToEvening();
    }

    private void TryAdvanceToEvening()
    {
        if (CurrentState != DayState.WorkShift)
        {
            return;
        }

        if (CarsServed >= RegularCarQuota && AreAllGuestsServed)
        {
            AdvanceTo(DayState.Evening, "квота и все гости — ровно 18:00");
        }
    }

    /// <summary>Гость опоздал и приехал уже в вечер (страховка от мягкого замка).</summary>
    public void ForceEvening()
    {
        if (CurrentState == DayState.WorkShift)
        {
            AdvanceTo(DayState.Evening, "принудительный вечер (страховка)");
        }
    }

    // --- ВЕЧЕР → НОЧЬ → СЛЕДУЮЩИЙ ДЕНЬ ---

    /// <summary>Вечернее событие (визит Котова и письмо) завершено → ночь.</summary>
    public void NotifyEveningCompleted()
    {
        if (CurrentState == DayState.Evening)
        {
            AdvanceTo(DayState.NightTransition, "письмо получено");
        }
    }

    /// <summary>Ночное событие (радио/силуэт/чужая строка) завершено → следующий день или конец среза.</summary>
    public void NotifyNightCompleted()
    {
        if (CurrentState != DayState.NightTransition)
        {
            return;
        }

        if (currentDay >= DayPlans.DayCount)
        {
            CompleteSlice();
            return;
        }

        BeginDay(currentDay + 1);
    }

    private void CompleteSlice()
    {
        if (sliceCompleted)
        {
            return;
        }

        sliceCompleted = true;
        AdvanceTo(DayState.NightTransition, "конец нулевой версии", silent: true);
        Debug.Log("[Snowpaw Station] Нулевая версия пройдена: три дня завершены.");
        SliceCompleted?.Invoke(currentDay);
    }

    private void BeginDay(int day)
    {
        currentDay = Mathf.Clamp(day, 1, DayPlans.DayCount);
        CarsServed = 0;
        stoveFueledToday = false;
        taskProgress.Clear();
        pendingGuests.Clear();
        CurrentState = DayState.Morning;
        InteractionLock.Reset();
        PlayerControlLock.Reset();

        DayChanged?.Invoke(currentDay);
        DayStarted?.Invoke(currentDay);
        PhaseChanged?.Invoke(currentDay, CurrentState);
        Debug.Log($"[Snowpaw Station] === День {currentDay}: Утро. Задачи: {DescribeTasks()} ===");
    }

    /// <summary>Запуск конкретного дня (для ручной проверки в редакторе).</summary>
    public void StartDay(int day)
    {
        if (!DayPlans.IsValidDay(day))
        {
            Debug.LogError($"DayLoopManager: нет дня {day} в нулевой версии.");
            return;
        }

        sliceCompleted = false;
        BeginDay(day);
    }

    private void AdvanceTo(DayState next, string reason, bool silent = false)
    {
        if (CurrentState == next)
        {
            return;
        }

        Debug.Log($"[Snowpaw Station] День {currentDay}: {CurrentState} → {next} ({reason}).");
        CurrentState = next;

        if (next == DayState.WorkShift)
        {
            RegisterDayGuests(DayPlans.GetGuestSchedule(currentDay));
        }

        if (!silent)
        {
            PhaseChanged?.Invoke(currentDay, next);
        }
    }

    // --- ПЕЧЬ / ДРОВА ---

    /// <summary>Полено отправлено в печь (StoveInteractable). Идёт в зачёт задачи и ясность письма №1.</summary>
    public void NotifyStoveFueled()
    {
        if (stoveFueledToday)
        {
            return;
        }

        stoveFueledToday = true;
        Debug.Log("[Snowpaw Station] Печь растоплена сегодня.");
        NotifyMorningTaskDone("stove_lit");
    }

    public bool StoveFueledToday => stoveFueledToday;

    /// <summary>Истинно, если печь сегодня уже топилась (для реплики Котова «Хорошо у тебя печь дышит»).</summary>
    public bool IsStoveLitToday()
    {
        return stoveFueledToday ||
               (StationHeatSystem.Active != null && StationHeatSystem.Active.CurrentHeat >= 30f);
    }

    /// <summary>Равнозначно событию события письма: вечер считается закрытым.</summary>
    public void OnLetterReceived()
    {
        NotifyEveningCompleted();
    }

    private DayPlan.MorningTask FindTask(string taskId)
    {
        List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(currentDay);
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].id == taskId)
            {
                return tasks[i];
            }
        }

        return null;
    }

    private string DescribeTasks()
    {
        List<DayPlan.MorningTask> tasks = DayPlans.GetMorningTasks(currentDay);
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < tasks.Count; i++)
        {
            if (i > 0)
            {
                builder.Append("; ");
            }

            builder.Append(tasks[i].label).Append(tasks[i].required ? "" : " (необязательно)");
        }

        return builder.ToString();
    }
}
