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

    // Marker component added to spawned ink objects so we can safely delete only drawings.
    // (This avoids needing a Unity Tag setup and prevents deleting unrelated LineRenderers.)
    private sealed class InkStroke : MonoBehaviour { }

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
                    if (newLine.GetComponent<InkStroke>() == null)
                        newLine.AddComponent<InkStroke>();
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

        // 5. PRESS C: Erase Everything
        if (Input.GetKeyDown(KeyCode.C))
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
    // Destroy only the ink objects we created.
    // FindObjectsByType is the modern API; FindObjectsOfType keeps older Unity versions compatible.
#if UNITY_2023_1_OR_NEWER
    var strokes = Object.FindObjectsByType<InkStroke>(FindObjectsSortMode.None);
#else
    var strokes = Object.FindObjectsOfType<InkStroke>();
#endif
    foreach (var stroke in strokes)
        Destroy(stroke.gameObject);

        Debug.Log("Board Cleared!");
    }
}