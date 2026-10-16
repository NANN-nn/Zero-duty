using UnityEngine;

[DisallowMultipleComponent]
public class SimpleCrosshair : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField, Range(1f, 4f)] private float thickness = 2f;
    [SerializeField, Range(2f, 16f)] private float size = 6f;
    [SerializeField, Range(0f, 8f)] private float gap = 2f;

    private void OnGUI()
    {
        if (MonitorViewManager.ActiveSession != null) return;
        Color previous = GUI.color;
        GUI.color = color;
        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;
        Texture2D texture = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(x - gap - size, y - thickness * 0.5f, size, thickness), texture);
        GUI.DrawTexture(new Rect(x + gap, y - thickness * 0.5f, size, thickness), texture);
        GUI.DrawTexture(new Rect(x - thickness * 0.5f, y - gap - size, thickness, size), texture);
        GUI.DrawTexture(new Rect(x - thickness * 0.5f, y + gap, thickness, size), texture);
        GUI.color = previous;
    }
}
