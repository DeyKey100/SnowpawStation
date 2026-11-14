using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Общая тема IMGUI-интерфейса: палитра, пиксельный шрифт и рамки-панели в духе PS1.
/// Текстуры рамок рисуются кодом (9-slice, точечная фильтрация), ассеты не нужны.
/// Вызывать только из OnGUI.
/// </summary>
public static class UiTheme
{
    // Палитра: тёмная зимняя ночь + тёплый свет печи.
    public static readonly Color Night = new Color(0.06f, 0.08f, 0.13f, 0.95f);
    public static readonly Color NightSoft = new Color(0.11f, 0.14f, 0.21f, 0.95f);
    public static readonly Color Outline = new Color(0.02f, 0.025f, 0.045f, 1f);
    public static readonly Color Cream = new Color(0.94f, 0.87f, 0.73f, 1f);
    public static readonly Color Ember = new Color(0.97f, 0.64f, 0.30f, 1f);
    public static readonly Color EmberDark = new Color(0.27f, 0.15f, 0.09f, 0.97f);
    public static readonly Color Ice = new Color(0.66f, 0.85f, 0.96f, 1f);
    public static readonly Color Text = new Color(0.97f, 0.95f, 0.90f, 1f);
    public static readonly Color Muted = new Color(0.63f, 0.68f, 0.76f, 1f);
    public static readonly Color Frost = new Color(0.36f, 0.43f, 0.54f, 1f);

    private const int FrameCells = 12;
    private const int FrameBorderCells = 4;

    private static readonly Dictionary<string, GUIStyle> frames = new Dictionary<string, GUIStyle>();
    private static Font font;
    private static bool fontLoaded;
    private static Texture2D white;

    /// <summary>Масштаб интерфейса от высоты экрана (1 при 900p).</summary>
    public static float Scale => Mathf.Clamp(Screen.height / 900f, 1f, 2.2f);

    /// <summary>Размер одного «пикселя» рамки в экранных пикселях.</summary>
    public static int PixelSize => Mathf.Max(1, Mathf.RoundToInt(Scale * 2f));

    public static Font Font
    {
        get
        {
            if (!fontLoaded)
            {
                fontLoaded = true;
                font = Resources.Load<Font>("Fonts/PixelifySans");
            }

            return font;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        frames.Clear();
        font = null;
        fontLoaded = false;
        white = null;
    }

    /// <summary>Базовый текстовый стиль темы.</summary>
    public static GUIStyle Label(float size, Color color, FontStyle fontStyle = FontStyle.Normal,
        TextAnchor alignment = TextAnchor.UpperLeft, bool wordWrap = false)
    {
        GUIStyle style = new GUIStyle(GUIStyle.none)
        {
            font = Font,
            fontSize = Mathf.RoundToInt(size * Scale),
            fontStyle = fontStyle,
            alignment = alignment,
            wordWrap = wordWrap,
            richText = false,
        };
        style.normal.textColor = color;
        return style;
    }

    /// <summary>Панель со срезанными углами: тёмный контур, цветная рамка, заливка.</summary>
    public static void Frame(Rect rect, Color fill, Color border)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        FrameStyle(fill, border, PixelSize).Draw(rect, GUIContent.none, false, false, false, false);
    }

    public static void Fill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (white == null)
        {
            white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }

        Color previous = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, color.a * previous.a);
        GUI.DrawTexture(rect, white);
        GUI.color = previous;
    }

    private static GUIStyle FrameStyle(Color fill, Color border, int pixel)
    {
        string key = $"{(Color32)fill}|{(Color32)border}|{pixel}";
        if (frames.TryGetValue(key, out GUIStyle style) && style.normal.background != null)
        {
            return style;
        }

        int edge = FrameBorderCells * pixel;
        style = new GUIStyle
        {
            border = new RectOffset(edge, edge, edge, edge),
        };
        style.normal.background = BuildFrameTexture(fill, border, pixel);
        frames[key] = style;
        return style;
    }

    private static Texture2D BuildFrameTexture(Color fill, Color border, int pixel)
    {
        int size = FrameCells * pixel;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int cx = Mathf.Min(x / pixel, FrameCells - 1 - x / pixel);
                int cy = Mathf.Min(y / pixel, FrameCells - 1 - y / pixel);
                int depth = Mathf.Min(cx, cy);
                int corner = cx + cy;

                Color color;
                if (corner < 2) color = Color.clear;
                else if (depth == 0 || corner == 2) color = Outline;
                else if (depth == 1 || corner == 3) color = border;
                else color = fill;

                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
