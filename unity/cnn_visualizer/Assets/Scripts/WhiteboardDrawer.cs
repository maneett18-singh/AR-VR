using UnityEngine;
using System.Collections.Generic;

public class WhiteboardDrawer : MonoBehaviour
{
    [Header("Drawing Settings")]
    public GameObject linePrefab; 
    public LayerMask drawingLayer; 
    public float rayDistance = 100f; 
    public float offsetFromSurface = 0.06f; 

    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();

    void Update()
    {
        // 1. Raycast from the center of the viewport (Crosshair position)
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // 2. LEFT CLICK: Start Drawing
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, rayDistance, drawingLayer))
            {
                // Only allow drawing if we hit the Positive X-axis face
                if (IsPositiveXFace(hit))
                {
                    GameObject newLine = Instantiate(linePrefab);
                    currentLine = newLine.GetComponent<LineRenderer>();
                    points.Clear();
                }
            }
        }

        // 3. HOLD LEFT CLICK: Continue Drawing
        if (Input.GetMouseButton(0) && currentLine != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, rayDistance, drawingLayer))
            {
                // Ensure we stay on the Positive X face while moving the mouse
                if (IsPositiveXFace(hit))
                {
                    Vector3 hitPoint = hit.point + (hit.normal * offsetFromSurface);
                    
                    if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], hitPoint) > 0.01f)
                    {
                        points.Add(hitPoint);
                        currentLine.positionCount = points.Count;
                        currentLine.SetPosition(points.Count - 1, hitPoint);
                    }
                }
            }
        }

        // 4. RELEASE LEFT CLICK: Stop Drawing
        if (Input.GetMouseButtonUp(0))
        {
            currentLine = null;
        }

        // 5. RIGHT CLICK: Erase Everything
        if (Input.GetMouseButtonDown(1))
        {
            ClearBoard();
        }
    }

    // This logic restricts drawing to the face aligned with the Red Arrow (+X)
    bool IsPositiveXFace(RaycastHit hit)
    {
        // Dot product compares the surface normal to the board's local 'Right' direction
        float dotRight = Vector3.Dot(hit.normal, hit.transform.right);
        
        // 1.0 means perfectly aligned with +X. We use 0.9 to allow for slight angles
        return dotRight > 0.9f; 
    }

    public void ClearBoard()
    {
        // Find and destroy all ink objects
        LineRenderer[] allLines = GameObject.FindObjectsOfType<LineRenderer>();
        foreach (LineRenderer line in allLines)
        {
            Destroy(line.gameObject);
        }
        Debug.Log("Board Cleared!");
    }
}