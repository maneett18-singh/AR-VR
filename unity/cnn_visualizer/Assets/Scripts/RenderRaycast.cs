using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RaycastVisibleOnKey : MonoBehaviour
{
    [Header("Interaction")]
    public Transform holdPoint;

    // 🔌 ADDED
    private Wire heldWire;

    public Transform player;
    public Camera playerCamera;
    public float rayDistance = 5f;
    public LayerMask raycastLayers = ~0;
    public KeyCode highlightKey = KeyCode.R;

    [HideInInspector] public GameObject highlightedObject;
    [HideInInspector] public GameObject lastHighlightedObject;

    private LineRenderer line;
    private Renderer currentRenderer;
    private Color originalColor;
    private Material lineMaterial;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.enabled = false;
        line.startWidth = 0.02f;
        line.endWidth = 0.02f;

        lineMaterial = new Material(Shader.Find("Unlit/Color"));
        lineMaterial.color = Color.orange;
        line.material = lineMaterial;

        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    void Update()
    {
        HandleInteractionInput();

        if (!Input.GetKey(highlightKey))
        {
            line.enabled = false;
            ResetHighlight();
            return;
        }

        line.enabled = true;

        // 🔵 ORIGINAL RAY (UNCHANGED)
        Vector3 start = transform.position;
        Vector3 direction = playerCamera.transform.forward;
        Vector3 end = start + direction * rayDistance;

        if (Physics.Raycast(start, direction, out RaycastHit hit, rayDistance, raycastLayers, QueryTriggerInteraction.Collide))
        {
            end = hit.point;
            HandleHighlighting(hit);
        }
        else
        {
            ResetHighlight();
        }

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    // =============================
    // INTERACTION (E KEY)
    // =============================
    private void HandleInteractionInput()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        // Must be highlighting something
        GameObject target = highlightedObject;
        if (target == null) return;

        // Find PlugIdentity on target or its parents (this is how we identify which wire a plug belongs to)
        PlugIdentity plugId = target.GetComponent<PlugIdentity>();
        if (plugId == null) plugId = target.GetComponentInParent<PlugIdentity>();

        // --- CASE 1: If we are looking at a plug ---
        // Allow picking up (or switching) to the currently highlighted plug.
        if (plugId != null && !plugId.wireRoot.isLocked)
        {
            if (heldWire == null || heldWire != plugId.wireRoot)
            {
                heldWire = plugId.wireRoot;
                heldWire.PickUp(holdPoint);
                Debug.Log("Holding wire: " + heldWire.wireIndex);
            }
            return;
        }

        // --- CASE 2: If we are looking at a socket and holding a wire ---
        Socket socket = target.GetComponent<Socket>();
        if (socket != null && heldWire != null)
        {
            // Let Socket.TryPlug decide whether it can be replaced
            socket.TryPlug(heldWire);
            heldWire = null; // release from hand after trying to plug
            Debug.Log("Tried plugging wire into socket");
            return;
        }

        // --- CASE 3: Zipline ---
        if (target.CompareTag("Zipline"))
        {
            Zipline zipline = target.GetComponentInParent<Zipline>();
            if (zipline != null) zipline.Attach(player);
        }
    }

    // =============================
    // WIRE PICKUP
    // =============================
    private void PickUpWire(Wire wire)
    {
        heldWire = wire;

        Rigidbody rb = wire.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        wire.transform.SetParent(holdPoint);
        wire.transform.localPosition = Vector3.zero;

        Debug.Log("Wire picked up");
    }

    // =============================
    // HIGHLIGHT (UNCHANGED)
    // =============================
    private void HandleHighlighting(RaycastHit hit)
    {
        Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
        if (hitRenderer != null && hitRenderer != currentRenderer)
        {
            ResetHighlight();
            // Don't overwrite socket status colors (red/green) with highlight yellow.
            // We still set highlightedObject so interaction works.
            if (hit.collider.GetComponent<Socket>() == null)
            {
                currentRenderer = hitRenderer;
                originalColor = currentRenderer.material.color;
                currentRenderer.material.color = Color.yellow;
            }
            highlightedObject = hitRenderer.gameObject;
            lastHighlightedObject = hitRenderer.gameObject;
        }
    }

    private void ResetHighlight()
    {
        if (currentRenderer != null)
        {
            currentRenderer.material.color = originalColor;
            currentRenderer = null;
            highlightedObject = null;
        }
    }
}
