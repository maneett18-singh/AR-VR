using UnityEngine;
using UnityEngine.UI;

public class GridGenerator : MonoBehaviour
{
    public RawImage targetPanel; // the image panel
    public int rows = 28;
    public int cols = 28;
    public Color baseColor = new Color(1, 1, 1, 0.1f); // faint base grid color
    public Color lineColor = Color.red;

    public float lineThickness = 10f; // thickness of the grid lines

    private Image[,] cells;
    private GameObject gridParent;
    private GameObject lineParent;

    public void CreateGrid()
    {
        if (targetPanel == null) return;

        // Remove existing grid and lines
        Transform existingGrid = targetPanel.transform.Find("Grid");
        if (existingGrid != null) DestroyImmediate(existingGrid.gameObject);

        Transform existingLines = targetPanel.transform.Find("GridLines");
        if (existingLines != null) DestroyImmediate(existingLines.gameObject);

        gridParent = new GameObject("Grid");
        gridParent.transform.SetParent(targetPanel.transform, false);

        lineParent = new GameObject("GridLines");
        lineParent.transform.SetParent(targetPanel.transform, false);

        lineParent.transform.SetAsLastSibling(); // ensures it renders on top


        float width = targetPanel.rectTransform.rect.width;
        float height = targetPanel.rectTransform.rect.height;

        float cellWidth = width / cols;
        float cellHeight = height / rows;

        cells = new Image[rows, cols];

        // Create grid cells
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                GameObject cellGO = new GameObject($"Cell_{x}_{y}");
                cellGO.transform.SetParent(gridParent.transform, false);

                Image cellImage = cellGO.AddComponent<Image>();
                cellImage.color = baseColor;

                RectTransform rt = cellGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellWidth, cellHeight);

                rt.anchoredPosition = new Vector2(-width / 2 + x * cellWidth + cellWidth / 2,
                                                  -height / 2 + y * cellHeight + cellHeight / 2);

                cells[y, x] = cellImage;
            }
        }

        // Create vertical lines
        for (int x = 0; x <= cols; x++)
        {
            GameObject line = new GameObject($"VLine_{x}");
            line.transform.SetParent(lineParent.transform, false);

            Image lineImage = line.AddComponent<Image>();
            lineImage.color = lineColor;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(lineThickness, height); // thin vertical line
            rt.anchoredPosition = new Vector2(-width / 2 + x * cellWidth, 0);
        }

        // Create horizontal lines
        for (int y = 0; y <= rows; y++)
        {
            GameObject line = new GameObject($"HLine_{y}");
            line.transform.SetParent(lineParent.transform, false);

            Image lineImage = line.AddComponent<Image>();
            lineImage.color = lineColor;

            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, lineThickness); // thin horizontal line
            rt.anchoredPosition = new Vector2(0, -height / 2 + y * cellHeight);
        }
    }

    // Highlight a single cell dynamically
    public void HighlightCell(int row, int col, Color color)
    {
        if (cells == null) return;
        if (row < 0 || row >= rows || col < 0 || col >= cols) return;

        cells[row, col].color = color;
    }

    // Optional: reset all cells
    public void ResetGrid()
    {
        if (cells == null) return;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                cells[y, x].color = baseColor;
            }
        }
    }
}
