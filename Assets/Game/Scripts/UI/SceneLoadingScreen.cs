using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Чёрный экран с надписью «Загрузка» на время асинхронной смены сцены.
/// Объект создаётся кодом и не уничтожается при загрузке сцены.
/// </summary>
public class SceneLoadingScreen : MonoBehaviour
{
    public static bool IsLoading { get; private set; }

    private const float FadeInDuration = 0.15f;
    private const float FadeOutDuration = 0.25f;
    private const float MinimumVisibleDuration = 0.5f;
    private const float ProgressCap = 0.9f;

    private float screenAlpha;
    private float progress;
    private bool failed;
    private Texture2D blackTexture;
    private Texture2D barBackground;
    private Texture2D barFill;

    /// <summary>
    /// Загружает сцену под чёрным экраном. Повторные вызовы во время загрузки игнорируются.
    /// </summary>
    public static void Load(string sceneName)
    {
        Load(sceneName, false, Vector3.zero, 0f);
    }

    /// <summary>
    /// Загружает сцену под чёрным экраном и переносит игрока в заданную точку.
    /// </summary>
    public static void Load(string sceneName, Vector3 spawnPosition, float spawnYaw)
    {
        Load(sceneName, true, spawnPosition, spawnYaw);
    }

    private static void Load(string sceneName, bool movePlayer, Vector3 spawnPosition, float spawnYaw)
    {
        if (IsLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("SceneLoadingScreen: не задано имя сцены.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneLoadingScreen: сцена '{sceneName}' не добавлена в Build Settings.");
            return;
        }

        IsLoading = true;

        GameObject host = new GameObject("SceneLoadingScreen");
        DontDestroyOnLoad(host);

        SceneLoadingScreen screen = host.AddComponent<SceneLoadingScreen>();
        screen.StartCoroutine(screen.LoadRoutine(sceneName, movePlayer, spawnPosition, spawnYaw));
    }

    private void Awake()
    {
        blackTexture = MakeSolidTexture(Color.black);
        barBackground = MakeSolidTexture(new Color(1f, 1f, 1f, 0.18f));
        barFill = MakeSolidTexture(Color.white);
    }

    private void OnDestroy()
    {
        if (blackTexture != null) Destroy(blackTexture);
        if (barBackground != null) Destroy(barBackground);
        if (barFill != null) Destroy(barFill);
    }

    private IEnumerator LoadRoutine(string sceneName, bool movePlayer, Vector3 spawnPosition, float spawnYaw)
    {
        yield return FadeTo(1f, FadeInDuration);

        AsyncOperation operation = null;
        try
        {
            operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"SceneLoadingScreen: не удалось загрузить сцену '{sceneName}'. {exception.Message}");
        }

        if (operation == null)
        {
            yield return ShowFailure();
            yield break;
        }

        operation.allowSceneActivation = false;

        float visibleTime = 0f;
        while (operation.progress < ProgressCap)
        {
            progress = Mathf.Clamp01(operation.progress / ProgressCap);
            visibleTime += Time.unscaledDeltaTime;
            yield return null;
        }

        progress = 1f;

        while (visibleTime < MinimumVisibleDuration)
        {
            visibleTime += Time.unscaledDeltaTime;
            yield return null;
        }

        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        if (movePlayer)
        {
            MovePlayer(spawnPosition, spawnYaw);
        }

        // Дать новой сцене отрисовать первый кадр под чёрным экраном.
        yield return null;

        IsLoading = false;
        yield return FadeTo(0f, FadeOutDuration);

        Destroy(gameObject);
    }

    private static void MovePlayer(Vector3 spawnPosition, float spawnYaw)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("SceneLoadingScreen: игрок не найден, точка появления не применена.");
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;

        if (controllerWasEnabled)
        {
            controller.enabled = false;
        }

        player.transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0f, spawnYaw, 0f));

        if (controllerWasEnabled)
        {
            controller.enabled = true;
        }
    }

    private IEnumerator ShowFailure()
    {
        failed = true;
        yield return new WaitForSecondsRealtime(2.5f);
        IsLoading = false;
        yield return FadeTo(0f, FadeOutDuration);
        Destroy(gameObject);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = screenAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            screenAlpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        screenAlpha = targetAlpha;
    }

    private void OnGUI()
    {
        if (blackTexture == null || screenAlpha <= 0.001f)
        {
            return;
        }

        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, screenAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), blackTexture, ScaleMode.StretchToFill);

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            normal = { textColor = failed ? new Color(1f, 0.35f, 0.35f) : Color.white }
        };

        string title = failed ? "Не удалось загрузить сцену" : "Загрузка";
        Rect titleRect = new Rect(0f, Screen.height * 0.5f - 60f, Screen.width, 48f);
        GUI.Label(titleRect, title, titleStyle);

        if (failed)
        {
            GUI.color = previousColor;
            return;
        }

        float barWidth = Mathf.Min(360f, Screen.width - 80f);
        Rect barBackRect = new Rect(Screen.width * 0.5f - barWidth * 0.5f, Screen.height * 0.5f + 2f, barWidth, 8f);
        GUI.DrawTexture(barBackRect, barBackground, ScaleMode.StretchToFill);

        Rect barFillRect = new Rect(barBackRect.x, barBackRect.y, barWidth * progress, barBackRect.height);
        GUI.DrawTexture(barFillRect, barFill, ScaleMode.StretchToFill);

        GUIStyle percentStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            normal = { textColor = new Color(1f, 1f, 1f, 0.75f) }
        };

        Rect percentRect = new Rect(0f, barBackRect.y + 18f, Screen.width, 24f);
        GUI.Label(percentRect, $"{Mathf.RoundToInt(progress * 100f)}%", percentStyle);

        GUI.color = previousColor;
    }

    private static Texture2D MakeSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
