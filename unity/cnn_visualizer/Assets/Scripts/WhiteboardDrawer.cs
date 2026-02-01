using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


public class WhiteboardDrawer : MonoBehaviour
{
    public enum DrawMode
    {
        PressToDraw,
        HoverToDraw,
    }

    [Header("Drawing Settings")]
    public GameObject linePrefab; 
    public LayerMask drawingLayer; 
    public float rayDistance = 100f; 
    public float offsetFromSurface = 0.06f; 

    [Tooltip("Default stroke width applied to the spawned LineRenderer (VR usually needs thicker lines).")]
    [SerializeField] private float lineWidth = 0.02f;

    [Tooltip("If the prefab has no material, a fallback material will be created with this color.")]
    [SerializeField] private Color fallbackLineColor = Color.black;

    [Header("Debug")]
    [SerializeField] private bool logHitTarget = false;

    [SerializeField] private DrawMode drawMode = DrawMode.PressToDraw;

    [Header("Surface Filter (Optional)")]
    [Tooltip("If enabled, drawing only works on the face whose normal points along the hit transform's +X (right) direction.")]
    [SerializeField] private bool restrictToPositiveXFace = false;

    [Header("XR Input (Optional)")]
    [SerializeField] private Component drawRaySource; // XRRayInteractor or NearFarInteractor
    [SerializeField] private InputActionReference drawAction; // e.g., right trigger
    [SerializeField] private InputActionReference eraseAction; // e.g., B button

    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();
    private IXRRayProvider rayProvider;
    private XRRayInteractor xrRayInteractor;

    private bool warnedMissingRaySource;
    private bool warnedMissingLayerMask;
    private bool wasHittingDrawable;

    private static readonly int ShaderColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        RefreshRaySource();
    }

    private void OnEnable()
    {
        if (drawAction != null) drawAction.action?.Enable();
        if (eraseAction != null) eraseAction.action?.Enable();
    }

    private void OnDisable()
    {
        if (drawAction != null) drawAction.action?.Disable();
        if (eraseAction != null) eraseAction.action?.Disable();
    }

    private void OnValidate()
    {
        RefreshRaySource();
    }

    void Update()
    {
        if (linePrefab == null)
        {
            Debug.LogError("WhiteboardDrawer: Line Prefab is not assigned.");
            enabled = false;
            return;
        }

        bool doErase = eraseAction != null ? eraseAction.action.WasPressedThisFrame() : Input.GetMouseButtonDown(1);

        // If user changed the Draw Ray Source at runtime / after domain reload.
        if (drawRaySource != null && rayProvider == null && xrRayInteractor == null)
            RefreshRaySource();

        if (drawMode == DrawMode.HoverToDraw)
        {
            UpdateHoverDraw();
        }
        else
        {
            bool startDraw = drawAction != null ? drawAction.action.WasPressedThisFrame() : Input.GetMouseButtonDown(0);
            bool holdDraw = drawAction != null ? drawAction.action.IsPressed() : Input.GetMouseButton(0);
            bool endDraw = drawAction != null ? drawAction.action.WasReleasedThisFrame() : Input.GetMouseButtonUp(0);
            UpdatePressDraw(startDraw, holdDraw, endDraw);
        }

        if (doErase)
        {
            ClearBoard();
        }
    }

    private void RefreshRaySource()
    {
        rayProvider = null;
        xrRayInteractor = null;
        warnedMissingRaySource = false;

        if (drawRaySource == null)
            return;

        // Direct assignment
        rayProvider = drawRaySource as IXRRayProvider;
        xrRayInteractor = drawRaySource as XRRayInteractor;
        if (rayProvider != null || xrRayInteractor != null)
            return;

        // If user dragged a Transform/GameObject instead of the component, try resolving on same object.
        var providerOnObject = drawRaySource.GetComponent<IXRRayProvider>();
        if (providerOnObject != null)
        {
            rayProvider = providerOnObject;
            return;
        }

        var rayInteractorOnObject = drawRaySource.GetComponent<XRRayInteractor>();
        if (rayInteractorOnObject != null)
        {
            xrRayInteractor = rayInteractorOnObject;
            return;
        }

        // Last resort: search parent chain.
        var providerInParent = drawRaySource.GetComponentInParent<IXRRayProvider>();
        if (providerInParent != null)
        {
            rayProvider = providerInParent;
            return;
        }

        var rayInteractorInParent = drawRaySource.GetComponentInParent<XRRayInteractor>();
        if (rayInteractorInParent != null)
        {
            xrRayInteractor = rayInteractorInParent;
            return;
        }
    }

    private void UpdatePressDraw(bool startDraw, bool holdDraw, bool endDraw)
    {
        if (startDraw && TryGetHit(out RaycastHit hitStart))
        {
            if (!restrictToPositiveXFace || IsPositiveXFace(hitStart))
            {
                GameObject newLine = Instantiate(linePrefab);
                currentLine = newLine.GetComponent<LineRenderer>();
                if (currentLine == null)
                {
                    Debug.LogError("WhiteboardDrawer: Line Prefab must contain a LineRenderer component.");
                    Destroy(newLine);
                    return;
                }
                SetupLineRenderer(currentLine);
                points.Clear();
            }
        }

        if (holdDraw && currentLine != null && TryGetHit(out RaycastHit hitMove))
        {
            if (!restrictToPositiveXFace || IsPositiveXFace(hitMove))
            {
                Vector3 hitPoint = hitMove.point + (hitMove.normal * offsetFromSurface);

                if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], hitPoint) > 0.01f)
                {
                    points.Add(hitPoint);
                    currentLine.positionCount = points.Count;
                    currentLine.SetPosition(points.Count - 1, hitPoint);
                }
            }
        }

        if (endDraw)
        {
            currentLine = null;
        }
    }

    private void UpdateHoverDraw()
    {
        bool hitNow = TryGetHit(out RaycastHit hit);
        bool drawableNow = hitNow && (!restrictToPositiveXFace || IsPositiveXFace(hit));

        if (drawableNow)
        {
            Vector3 hitPoint = hit.point + (hit.normal * offsetFromSurface);

            // Start a new stroke when the laser first touches the drawable surface
            if (!wasHittingDrawable)
            {
                GameObject newLine = Instantiate(linePrefab);
                currentLine = newLine.GetComponent<LineRenderer>();
                if (currentLine == null)
                {
                    Debug.LogError("WhiteboardDrawer: Line Prefab must contain a LineRenderer component.");
                    Destroy(newLine);
                    return;
                }
                SetupLineRenderer(currentLine);
                points.Clear();

                if (logHitTarget)
                    Debug.Log($"WhiteboardDrawer: Start stroke on '{hit.collider.gameObject.name}' (layer {hit.collider.gameObject.layer}) at {hit.point}");

                points.Add(hitPoint);
                currentLine.positionCount = points.Count;
                currentLine.SetPosition(0, hitPoint);
            }
            else if (currentLine != null)
            {
                if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], hitPoint) > 0.01f)
                {
                    points.Add(hitPoint);
                    currentLine.positionCount = points.Count;
                    currentLine.SetPosition(points.Count - 1, hitPoint);
                }
            }
        }
        else
        {
            // End stroke when the laser leaves the drawable surface
            currentLine = null;
        }

        wasHittingDrawable = drawableNow;
    }

    private bool TryGetHit(out RaycastHit hit)
    {
        // Prefer using XR Ray Interactor's own raycast hit (this matches the laser visual)
        if (xrRayInteractor != null)
        {
            if (xrRayInteractor.TryGetCurrent3DRaycastHit(out hit))
                return true;
        }

        if (rayProvider != null)
        {
            var origin = rayProvider.GetOrCreateRayOrigin();
            if (origin != null)
            {
                Vector3 end = rayProvider.rayEndPoint;
                Vector3 dir = end - origin.position;
                float dist = dir.magnitude;
                // NearFarInteractor can collapse the "far ray" when you're very close.
                // If that happens, fall back to a forward ray so drawing still works.
                if (dist > 0.01f)
                    dir /= dist;
                else
                {
                    dir = origin.forward;
                    dist = rayDistance;
                }

                int mask = drawingLayer.value;
                if (mask == 0)
                {
                    if (!warnedMissingLayerMask)
                    {
                        warnedMissingLayerMask = true;
                        Debug.LogWarning("WhiteboardDrawer: 'Drawing Layer' is set to Nothing. Include your board layer (e.g., Board) or keep it as Everything.");
                    }
                    mask = Physics.DefaultRaycastLayers;
                }

                if (Physics.Raycast(origin.position, dir, out hit, dist + 0.01f, mask, QueryTriggerInteraction.Collide))
                    return true;
            }
        }
        else if (drawRaySource != null && xrRayInteractor == null && !warnedMissingRaySource)
        {
            warnedMissingRaySource = true;
            Debug.LogWarning($"WhiteboardDrawer: Draw Ray Source '{drawRaySource.name}' did not resolve to an XR ray provider. Assign the XR Ray Interactor component (or Near-Far Interactor). If you dragged a Transform, drag the Interactor component instead.");
        }

        var cam = Camera.main;
        if (cam == null)
        {
            hit = default;
            return false;
        }

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return Physics.Raycast(ray, out hit, rayDistance, drawingLayer, QueryTriggerInteraction.Collide);
    }

    private void SetupLineRenderer(LineRenderer line)
    {
        if (line == null)
            return;

        // Make sure points we set (world hit points) are interpreted correctly.
        line.useWorldSpace = true;

        // VR often needs thicker lines; ensure a minimum width.
        float width = Mathf.Max(0.0001f, lineWidth);
        if (line.startWidth < width) line.startWidth = width;
        if (line.endWidth < width) line.endWidth = width;

        // Make sure alignment doesn't hide the line.
        line.alignment = LineAlignment.View;

        // If the prefab has no material assigned, assign a simple visible one.
        if (line.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                var mat = new Material(shader);
                if (mat.HasProperty(ShaderColorId))
                    mat.SetColor(ShaderColorId, fallbackLineColor);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", fallbackLineColor);
                line.sharedMaterial = mat;
            }
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