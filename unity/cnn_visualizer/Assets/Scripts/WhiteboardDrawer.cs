using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.IO;
using System.Reflection;


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

    [Header("Gamepad / Cursor")]
    [Tooltip("When enabled, left stick will move a virtual cursor over the screen which can be used to draw.")]
    public bool useGamepadCursor = true;
    [Tooltip("Speed at which the virtual cursor moves across the viewport (units/sec).")]
    public float gamepadCursorSpeed = 1.0f;
    // virtual cursor in viewport coords (0..1)
    private Vector2 gamepadCursor = new Vector2(0.5f, 0.5f);
    [Tooltip("Saved image size (pixels)")]
    public int saveImageWidth = 1920;
    public int saveImageHeight = 1080;

    private LineRenderer currentLine;
    private List<Vector3> points = new List<Vector3>();
    // Keep track of instantiated line GameObjects so we can reliably clear them later
    private List<GameObject> spawnedLines = new List<GameObject>();
    private IXRRayProvider rayProvider;
    private XRRayInteractor xrRayInteractor;
    // Fallback for custom interactor types (e.g. Near-Far Interactor) that expose a
    // TryGetCurrent3DRaycastHit(out RaycastHit) method but do not derive from XRRayInteractor.
    private Component fallbackInteractorComponent;
    private MethodInfo fallbackTryGetHitMethod;

    private bool warnedMissingRaySource;
    private bool warnedMissingLayerMask;
    private bool wasHittingDrawable;
    [Header("Debug Visuals")]
    [Tooltip("Show a small reticle at the current ray hit point for debugging and alignment.")]
    public bool showReticle = true;
    [Tooltip("Reticle scale in world units.")]
    public float reticleScale = 0.02f;
    private GameObject reticle;

    private static readonly int ShaderColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        RefreshRaySource();
    }

    private void Start()
    {
        // Print which source we'll use at runtime for easier debugging.
        if (xrRayInteractor != null)
            Debug.Log($"WhiteboardDrawer: resolving XR Ray Interactor -> '{xrRayInteractor.gameObject.name}'");
        else if (rayProvider != null)
            Debug.Log($"WhiteboardDrawer: resolving Ray Provider -> '{(rayProvider as Component)?.gameObject.name ?? rayProvider.GetType().Name}'");
        else if (Camera.main != null)
            Debug.Log($"WhiteboardDrawer: no XR ray source assigned, using Camera.main -> '{Camera.main.gameObject.name}'");
        else
            Debug.LogWarning("WhiteboardDrawer: No XR ray source resolved and no Camera.main present.");
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

        // Keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearBoard();
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            SaveBoardImage();
        }

        // If user changed the Draw Ray Source at runtime / after domain reload.
        if (drawRaySource != null && rayProvider == null && xrRayInteractor == null)
            RefreshRaySource();

        // Support gamepad virtual cursor (left stick) and gamepad button for drawing
        var gp = Gamepad.current;

        if (useGamepadCursor && gp != null)
        {
            // Move virtual cursor
            Vector2 stick = gp.leftStick.ReadValue();
            // invert Y for natural viewport movement
            gamepadCursor += new Vector2(stick.x, stick.y) * (gamepadCursorSpeed * Time.deltaTime);
            gamepadCursor.x = Mathf.Clamp01(gamepadCursor.x);
            gamepadCursor.y = Mathf.Clamp01(gamepadCursor.y);
        }

        if (drawMode == DrawMode.HoverToDraw)
        {
            UpdateHoverDraw();
        }
        else
        {
            bool startDraw;
            bool holdDraw;
            bool endDraw;

            if (gp != null)
            {
                startDraw = gp.buttonSouth.wasPressedThisFrame; // A / Cross
                holdDraw = gp.buttonSouth.isPressed;
                endDraw = gp.buttonSouth.wasReleasedThisFrame;
            }
            else
            {
                startDraw = drawAction != null ? drawAction.action.WasPressedThisFrame() : Input.GetMouseButtonDown(0);
                holdDraw = drawAction != null ? drawAction.action.IsPressed() : Input.GetMouseButton(0);
                endDraw = drawAction != null ? drawAction.action.WasReleasedThisFrame() : Input.GetMouseButtonUp(0);
            }

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

        // If user assigned a GameObject that isn't an XRRayInteractor but has an interactor
        // implementing TryGetCurrent3DRaycastHit, detect and cache it via reflection.
        if (drawRaySource != null)
        {
            var comps = drawRaySource.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var mi = c.GetType().GetMethod("TryGetCurrent3DRaycastHit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    // Verify signature: returns bool and has one out parameter of type RaycastHit
                    var pars = mi.GetParameters();
                    if (mi.ReturnType == typeof(bool) && pars.Length == 1 && pars[0].ParameterType == typeof(RaycastHit).MakeByRefType())
                    {
                        fallbackInteractorComponent = c;
                        fallbackTryGetHitMethod = mi;
                        Debug.Log($"WhiteboardDrawer: Using fallback interactor '{c.gameObject.name}' ({c.GetType().Name}) for hit queries.");
                        return;
                    }
                }
            }
        }

        // If the assigned Draw Ray Source didn't resolve to a provider/interactor,
        // try to find a suitable interactor/provider anywhere in the scene as a helpful fallback.
        if (rayProvider == null && xrRayInteractor == null)
        {
            // Prefer explicit XRRayInteractor components (common when using XR Interaction Toolkit).
            var allXRRay = Object.FindObjectsOfType<XRRayInteractor>();
            foreach (var xr in allXRRay)
            {
                if (xr != null && xr.enabled && xr.gameObject.activeInHierarchy)
                {
                    xrRayInteractor = xr;
                    Debug.Log($"WhiteboardDrawer: Auto-selected XR Ray Interactor '{xr.gameObject.name}' as Draw Ray Source.");
                    return;
                }
            }

            // Next, look for any component that implements IXRRayProvider.
            var allMono = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var m in allMono)
            {
                var provider = m as IXRRayProvider;
                if (provider != null)
                {
                    rayProvider = provider;
                    Debug.Log($"WhiteboardDrawer: Auto-selected Ray Provider on '{(m as Component).gameObject.name}' as Draw Ray Source.");
                    return;
                }
            }

            // Last-resort: search the scene for any component that exposes TryGetCurrent3DRaycastHit
            // (covers custom interactors like Near-Far Interactor that don't implement IXRRayProvider).
            foreach (var mono in allMono)
            {
                if (mono == null) continue;
                var mi = mono.GetType().GetMethod("TryGetCurrent3DRaycastHit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi != null)
                {
                    var pars = mi.GetParameters();
                    if (mi.ReturnType == typeof(bool) && pars.Length == 1 && pars[0].ParameterType == typeof(RaycastHit).MakeByRefType())
                    {
                        fallbackInteractorComponent = mono as Component;
                        fallbackTryGetHitMethod = mi;
                        Debug.Log($"WhiteboardDrawer: Auto-selected fallback interactor '{fallbackInteractorComponent.gameObject.name}' ({fallbackInteractorComponent.GetType().Name}) for hit queries.");
                        return;
                    }
                }
            }
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
                // track spawned line for easy clearing later
                spawnedLines.Add(newLine);
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
                // track spawned line for easy clearing later
                spawnedLines.Add(newLine);
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
        // If using the gamepad virtual cursor, project a ray from the main camera using the cursor viewport position.
        if (useGamepadCursor)
        {
            var gp = Gamepad.current;
            bool hasStickInput = gp != null || Mathf.Abs(Input.GetAxis("Horizontal")) > 0f || Mathf.Abs(Input.GetAxis("Vertical")) > 0f;
            if (gp != null || hasStickInput)
            {
                Camera camLocal = Camera.main;
                if (camLocal != null)
                {
                    Ray rayLocal = camLocal.ViewportPointToRay(new Vector3(gamepadCursor.x, gamepadCursor.y, 0f));
                    if (Physics.Raycast(rayLocal, out hit, rayDistance, drawingLayer, QueryTriggerInteraction.Collide))
                    {
                        UpdateReticle(hit);
                        return true;
                    }
                }
            }
        }

        // Prefer using XR Ray Interactor's own raycast hit (this matches the laser visual)
        if (xrRayInteractor != null)
        {
            if (xrRayInteractor.TryGetCurrent3DRaycastHit(out hit))
            {
                UpdateReticle(hit);
                return true;
            }
        }

        // If a fallback interactor component was detected that exposes TryGetCurrent3DRaycastHit,
        // invoke it via reflection to get the hit. This allows support for custom interactors
        // (e.g. Near-Far Interactor) that aren't of type XRRayInteractor.
        if (fallbackInteractorComponent != null && fallbackTryGetHitMethod != null)
        {
            object[] args = new object[] { default(RaycastHit) };
            try
            {
                bool ok = (bool)fallbackTryGetHitMethod.Invoke(fallbackInteractorComponent, args);
                if (ok)
                {
                    hit = (RaycastHit)args[0];
                    UpdateReticle(hit);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                // If reflection fails for some reason, clear the fallback to avoid repeated exceptions.
                Debug.LogWarning($"WhiteboardDrawer: fallback interactor invocation failed: {ex.Message}");
                fallbackInteractorComponent = null;
                fallbackTryGetHitMethod = null;
            }
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
                {
                    UpdateReticle(hit);
                    return true;
                }
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
        if (Physics.Raycast(ray, out hit, rayDistance, drawingLayer, QueryTriggerInteraction.Collide))
        {
            UpdateReticle(hit);
            return true;
        }

        // No hit anywhere - ensure reticle is hidden
        if (reticle != null)
            reticle.SetActive(false);

        return false;
    }

    private void EnsureReticle()
    {
        if (!showReticle) return;
        if (reticle != null) return;
        reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        // remove collider
        var col = reticle.GetComponent<Collider>();
        if (col != null) Destroy(col);
        reticle.name = "WhiteboardReticle";
        reticle.transform.localScale = Vector3.one * reticleScale;
        var mr = reticle.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            mr.sharedMaterial = new Material(shader);
            mr.sharedMaterial.color = Color.red;
        }
        reticle.SetActive(false);
        // don't save to scene
        reticle.hideFlags = HideFlags.DontSave;
    }

    private void UpdateReticle(RaycastHit hit)
    {
        if (!showReticle) return;
        EnsureReticle();
        if (reticle == null) return;
        reticle.SetActive(true);
        reticle.transform.position = hit.point + (hit.normal * offsetFromSurface);
        reticle.transform.rotation = Quaternion.LookRotation(hit.normal);
        reticle.transform.localScale = Vector3.one * reticleScale;
        if (logHitTarget)
            Debug.Log($"WhiteboardDrawer: Reticle at {hit.point} on {hit.collider.gameObject.name}");
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
        // Destroy only the line objects we created.
        // We track spawned line GameObjects in `spawnedLines` so we can reliably remove them.
        for (int i = spawnedLines.Count - 1; i >= 0; --i)
        {
            var go = spawnedLines[i];
            if (go != null)
                Destroy(go);
        }
        spawnedLines.Clear();

        // Fallback: if the user didn't use the tracked flow (or older scenes exist),
        // remove LineRenderer clones that match the prefab name (common Unity naming: "PrefabName(Clone)").
        if (linePrefab != null)
        {
            var allLineRenderers = Object.FindObjectsOfType<LineRenderer>();
            foreach (var lr in allLineRenderers)
            {
                if (lr == null || lr.gameObject == null) continue;
                if (lr.gameObject.name.StartsWith(linePrefab.name))
                    Destroy(lr.gameObject);
            }
        }

        Debug.Log("Board Cleared!");
    }

    private void SaveBoardImage()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("SaveBoardImage: No main camera found.");
            return;
        }

        int w = Mathf.Max(16, saveImageWidth);
        int h = Mathf.Max(16, saveImageHeight);
        var rt = new RenderTexture(w, h, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        RenderTexture.active = rt;
        cam.Render();

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = prev;
        RenderTexture.active = null;
        Destroy(rt);

        byte[] png = tex.EncodeToPNG();
        Destroy(tex);

        string folder = Application.persistentDataPath;
        string filename = Path.Combine(folder, $"whiteboard_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
        try
        {
            File.WriteAllBytes(filename, png);
            Debug.Log($"Saved whiteboard image to: {filename}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save image: {ex.Message}");
        }
    }
}