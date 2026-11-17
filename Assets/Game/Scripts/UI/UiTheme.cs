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
                font = Resources.Load<Font>("Fonts/CrispPixel");
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
        TextAnchor alignment = TextAnchor.UpperLeft, bool wordWrap = false, bool scaleWithScreen = true)
    {
        GUIStyle style = new GUIStyle(GUIStyle.none)
        {
            font = Font,
            fontSize = Mathf.RoundToInt(size * (scaleWithScreen ? Scale : 1f)),
            fontStyle = fontStyle,
            alignment = alignment,
            wordWrap = wordWrap,
            richText = false,
        };
        style.normal.textColor = color;
        return style;
    }

    /// <summary>Высота текста с учётом метрик шрифта и переноса, с запасом для краёв глифов.</summary>
    public static float TextHeight(GUIStyle style, string text, float width)
    {
        return Mathf.Ceil(Mathf.Max(style.lineHeight,
            style.CalcHeight(new GUIContent(text), Mathf.Max(1f, width)))) + 4f;
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

    /// <summary>Кнопка темы: рамка загорается под курсором. Возвращает true по клику.</summary>
    public static bool Button(Rect rect, string text, float fontSize = 18f, bool active = false)
    {
        bool hover = rect.Contains(Event.current.mousePosition);
        bool lit = hover || active;
        Frame(rect, lit ? EmberDark : NightSoft, lit ? Ember : Frost);
        GUI.Label(rect, text, Label(fontSize, lit ? Cream : Text, FontStyle.Bold, TextAnchor.MiddleCenter));
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }

    /// <summary>Горизонтальный ползунок 0..1 в стиле темы.</summary>
    public static float Slider(Rect rect, float value)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;
        float pixel = PixelSize;
        float knob = pixel * 6f;
        float travel = Mathf.Max(1f, rect.width - knob);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button == 0 && rect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    value = Mathf.Clamp01((e.mousePosition.x - rect.x - knob * 0.5f) / travel);
                    GUI.changed = true;
                    e.Use();
                }
                break;
            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    value = Mathf.Clamp01((e.mousePosition.x - rect.x - knob * 0.5f) / travel);
                    GUI.changed = true;
                    e.Use();
                }
                break;
            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;
            case EventType.Repaint:
                value = Mathf.Clamp01(value);
                float trackHeight = pixel * 4f;
                Rect track = new Rect(rect.x, rect.center.y - trackHeight * 0.5f, rect.width, trackHeight);
                Fill(track, Outline);
                Rect inner = new Rect(track.x + pixel, track.y + pixel, track.width - pixel * 2f, track.height - pixel * 2f);
                Fill(inner, NightSoft);
                Fill(new Rect(inner.x, inner.y, inner.width * value, inner.height), Ember);
                Rect knobRect = new Rect(rect.x + travel * value, rect.center.y - pixel * 5f, knob, pixel * 10f);
                Fill(knobRect, Outline);
                Fill(new Rect(knobRect.x + pixel, knobRect.y + pixel, knobRect.width - pixel * 2f, knobRect.height - pixel * 2f), Cream);
                break;
        }

        return value;
    }

    /// <summary>Полоса-индикатор 0..1 с пиксельной обводкой и делениями.</summary>
    public static void Bar(Rect rect, float value, Color color)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        float pixel = PixelSize;
        Fill(rect, Outline);
        Rect inner = new Rect(rect.x + pixel, rect.y + pixel, rect.width - pixel * 2f, rect.height - pixel * 2f);
        Fill(inner, NightSoft);
        Fill(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(value), inner.height), color);
        // Блик сверху и деления по четвертям — чтобы полоса читалась как «прибор».
        Fill(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(value), pixel), new Color(1f, 1f, 1f, 0.22f));
        for (int i = 1; i < 4; i++)
        {
            Fill(new Rect(inner.x + inner.width * i / 4f - pixel * 0.5f, inner.y, pixel, inner.height), new Color(0f, 0f, 0f, 0.35f));
        }
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
