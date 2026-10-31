using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrosshairHud : MonoBehaviour
{
    [SerializeField, Min(1f)] private float dotSize = 4f;
    [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.85f);

    private Texture2D dotTexture;

    private void Awake()
    {
        dotTexture = new Texture2D(1, 1);
        dotTexture.SetPixel(0, 0, Color.white);
        dotTexture.Apply();
    }

    private void OnDestroy()
    {
        if (dotTexture != null)
        {
            Destroy(dotTexture);
            dotTexture = null;
        }
    }

    private void OnGUI()
    {
        if (PauseMenu.IsPaused || dotTexture == null)
        {
            return;
        }

        float size = Mathf.Max(1f, dotSize);
        Rect dotRect = new Rect(
            (Screen.width - size) * 0.5f,
            (Screen.height - size) * 0.5f,
            size,
            size);

        Color previousColor = GUI.color;
        GUI.color = dotColor;
        GUI.DrawTexture(dotRect, dotTexture);
        GUI.color = previousColor;
    }
}
