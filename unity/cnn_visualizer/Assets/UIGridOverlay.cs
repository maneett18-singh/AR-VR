using UnityEngine;
using UnityEngine.UI;

public class UIGridOverlay : MonoBehaviour
{
    public int rows = 28;
    public int columns = 28;
    public float lineThickness = 1f;
    public Color gridColor = new Color(1, 1, 1, 0.25f);

    RectTransform rect;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        GenerateGrid();
    }

    void GenerateGrid()
    {
        float width = rect.rect.width;
        float height = rect.rect.height;

        float cellWidth = width / columns;
        float cellHeight = height / rows;

        // Vertical lines
        for (int i = 0; i <= columns; i++)
        {
            CreateLine(
                new Vector2(-width / 2 + i * cellWidth, 0),
                new Vector2(lineThickness, height)
            );
        }

        // Horizontal lines
        for (int j = 0; j <= rows; j++)
        {
            CreateLine(
                new Vector2(0, -height / 2 + j * cellHeight),
                new Vector2(width, lineThickness)
            );
        }
    }

    void CreateLine(Vector2 localPos, Vector2 size)
    {
        GameObject line = new GameObject("GridLine", typeof(Image));
        line.transform.SetParent(transform, false);

        Image img = line.GetComponent<Image>();
        img.color = gridColor;

        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = localPos;
    }
}
