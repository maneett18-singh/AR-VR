using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using System.Reflection;

// Lightweight external drawer that reads the right-hand trigger and a controller ray
// and performs drawing similar to WhiteboardDrawer without changing the original script.
// Usage: attach to a GameObject (e.g., DrawingManager), assign Line Prefab and Draw Source
// (an XR Ray Interactor component or a controller Transform). This script will use the
// right-hand trigger (XRNode.RightHand) to start/hold/end strokes.
public class ExternalWhiteboardDrawer : MonoBehaviour
{
    [Header("Drawing Settings")]
    public GameObject linePrefab;
    public LayerMask drawingLayer = ~0;
    public float rayDistance = 20f;
    public float offsetFromSurface = 0.06f;
    public float lineWidth = 0.02f;
    public Color fallbackLineColor = Color.black;

    [Header("Ray Source")]
    [Tooltip("Assign the controller Transform (or a GameObject that has a ray interactor component).")]
    public Component drawRaySource; // could be XRRayInteractor or a Transform

    [Header("Debug")]
    public bool showReticle = true;
    public float reticleScale = 0.02f;

    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();
    private List<GameObject> spawnedLines = new List<GameObject>();

    // For reflection (if user assigned an interactor component with TryGetCurrent3DRaycastHit)
    private Component fallbackInteractorComponent;
    private MethodInfo fallbackTryGetHitMethod;

    private GameObject reticle;
    private bool wasPressedLastFrame = false;

    void Start()
    {
        if (linePrefab == null)
            Debug.LogError("ExternalWhiteboardDrawer: linePrefab not assigned.");

        // Try to detect interactor method on the assigned drawRaySource
        if (drawRaySource != null)
            DetectFallbackInteractor(drawRaySource);
    }

    void Update()
    {
        // Read right-hand trigger button
        var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        bool pressed = false;
        if (rightDevice.isValid)
        {
            rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);
        }

        // Manage input transitions
        bool start = pressed && !wasPressedLastFrame;
        bool hold = pressed;
        bool end = !pressed && wasPressedLastFrame;

        if (start)
            TryStartStroke();
        if (hold)
            ContinueStroke();
        if (end)
            EndStroke();

        wasPressedLastFrame = pressed;
    }

    private void DetectFallbackInteractor(Component comp)
    {
        if (comp == null) return;
        var mi = comp.GetType().GetMethod("TryGetCurrent3DRaycastHit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null)
        {
            var pars = mi.GetParameters();
            if (mi.ReturnType == typeof(bool) && pars.Length == 1 && pars[0].ParameterType == typeof(RaycastHit).MakeByRefType())
            {
                fallbackInteractorComponent = comp;
                fallbackTryGetHitMethod = mi;
                Debug.Log($"ExternalWhiteboardDrawer: Found fallback interactor '{comp.gameObject.name}' ({comp.GetType().Name}).");
            }
        }
    }

    private bool TryGetHit(out RaycastHit hit)
    {
        // If we have an interactor with TryGetCurrent3DRaycastHit, invoke it
        if (fallbackInteractorComponent != null && fallbackTryGetHitMethod != null)
        {
            object[] args = new object[] { default(RaycastHit) };
            try
            {
                bool ok = (bool)fallbackTryGetHitMethod.Invoke(fallbackInteractorComponent, args);
                if (ok)
                {
                    hit = (RaycastHit)args[0];
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("ExternalWhiteboardDrawer: fallback interactor invocation failed: " + ex.Message);
                fallbackInteractorComponent = null;
                fallbackTryGetHitMethod = null;
            }
        }

        // Otherwise, do a simple raycast from the provided Transform forward
        if (drawRaySource != null && (drawRaySource is Transform || (drawRaySource as Component)?.transform != null))
        {
            Transform t = drawRaySource is Transform ? (Transform)drawRaySource : (drawRaySource as Component).transform;
            if (t != null)
            {
                Ray r = new Ray(t.position, t.forward);
                if (Physics.Raycast(r, out hit, rayDistance, drawingLayer, QueryTriggerInteraction.Collide))
                    return true;
            }
        }

        // Fallback: center camera ray
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(r, out hit, rayDistance, drawingLayer, QueryTriggerInteraction.Collide))
                return true;
        }

        hit = default;
        return false;
    }

    private void TryStartStroke()
    {
        if (linePrefab == null) return;
        if (TryGetHit(out RaycastHit hit))
        {
            GameObject newLine = Instantiate(linePrefab);
            currentLine = newLine.GetComponent<LineRenderer>();
            if (currentLine == null)
            {
                Debug.LogError("ExternalWhiteboardDrawer: Line Prefab must contain a LineRenderer component.");
                Destroy(newLine);
                return;
            }
            SetupLineRenderer(currentLine);
            points.Clear();
            spawnedLines.Add(newLine);
            Vector3 p = hit.point + hit.normal * offsetFromSurface;
            points.Add(p);
            currentLine.positionCount = points.Count;
            currentLine.SetPosition(0, p);
        }
    }

    private void ContinueStroke()
    {
        if (currentLine == null) return;
        if (TryGetHit(out RaycastHit hit))
        {
            Vector3 p = hit.point + hit.normal * offsetFromSurface;
            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], p) > 0.01f)
            {
                points.Add(p);
                currentLine.positionCount = points.Count;
                currentLine.SetPosition(points.Count - 1, p);
            }
        }
    }

    private void EndStroke()
    {
        currentLine = null;
    }

    private void SetupLineRenderer(LineRenderer line)
    {
        if (line == null) return;
        line.useWorldSpace = true;
        float width = Mathf.Max(0.0001f, lineWidth);
        line.startWidth = width;
        line.endWidth = width;
        line.alignment = LineAlignment.View;
        if (line.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fallbackLineColor);
                else if (mat.HasProperty("_Color")) mat.SetColor("_Color", fallbackLineColor);
                line.sharedMaterial = mat;
            }
        }
    }

    // Optional: public helper to clear lines (keeps parity with existing WhiteboardDrawer)
    public void ClearBoard()
    {
        for (int i = spawnedLines.Count - 1; i >= 0; --i)
        {
            var go = spawnedLines[i];
            if (go != null) Destroy(go);
        }
        spawnedLines.Clear();
    }
}
