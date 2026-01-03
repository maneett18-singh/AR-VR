using UnityEngine;
using System.Collections.Generic;

public class SaberPainter : MonoBehaviour
{
    public Camera cam;
    public Transform strokesParent;
    public Material lineMaterial;
    public float lineWidth = 0.05f;
    public LayerMask drawLayers;

    SaberStroke currentStroke;
    List<SaberStroke> allStrokes = new();

    void Start()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && RayHit(out var p)) StartStroke(p);
        if (Input.GetMouseButton(0) && currentStroke && RayHit(out var q)) currentStroke.AddPoint(q);
        if (Input.GetMouseButtonUp(0)) currentStroke = null;
    }

    bool RayHit(out Vector3 hit)
    {
        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(r, out RaycastHit h, 100f, drawLayers))
        { hit = h.point + h.normal * 0.001f; return true; }
        hit = Vector3.zero; return false;
    }

    void StartStroke(Vector3 start)
    {
        var go = new GameObject("Stroke", typeof(LineRenderer), typeof(SaberStroke));
        go.transform.SetParent(strokesParent, true);
        var s = go.GetComponent<SaberStroke>();
        s.Setup(lineMaterial, lineWidth);
        s.AddPoint(start);
        currentStroke = s;
        allStrokes.Add(s);
    }

    public void ClearAll()
    {
        foreach (var s in allStrokes) if (s) Destroy(s.gameObject);
        allStrokes.Clear();
    }
}
