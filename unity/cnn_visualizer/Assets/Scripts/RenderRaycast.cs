using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RaycastVisibleOnKey : MonoBehaviour
{
    [Header("Settings")]
    public float rayDistance = 5f;
    public LayerMask raycastLayers = ~0; // all layers by default
    public KeyCode highlightKey = KeyCode.R;

    [HideInInspector] public GameObject highlightedObject;
    [HideInInspector] public GameObject lastHighlightedObject; // NEW: store last highlighted

    private LineRenderer line;
    private Renderer currentRenderer;
    private Color originalColor;
    private Material lineMaterial;

    void Awake()
    {
        // Initialize LineRenderer
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.enabled = false;
        line.startWidth = 0.02f;
        line.endWidth = 0.02f;

        // Create a single material for the line (do not create every frame)
        lineMaterial = new Material(Shader.Find("Unlit/Color"));
        lineMaterial.color = Color.orange;
        line.material = lineMaterial;
    }

    void Update()
    {
        // Detect E key
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("E pressed");
            GameObject objToLog = highlightedObject != null ? highlightedObject : lastHighlightedObject;
            if (objToLog != null)
                Debug.Log("Currently highlighted object: " + objToLog.name);
            else
                Debug.Log("No object highlighted");
        }

        // Only enable highlighting when the key is held
        if (!Input.GetKey(highlightKey))
        {
            line.enabled = false;
            ResetHighlight();
            return;
        }

        line.enabled = true;

        // Ray origin and direction: directly in front of the player
        Vector3 start = transform.position;
        Vector3 direction = transform.forward;
        Vector3 end = start + direction * rayDistance;

        // Perform raycast
        if (Physics.Raycast(start, direction, out RaycastHit hit, rayDistance, raycastLayers))
        {
            end = hit.point;

            // Log hit
            Debug.Log("Ray hit: " + hit.collider.name);

            Renderer hitRenderer = hit.collider.GetComponent<Renderer>();

            if (hitRenderer != null && hitRenderer != currentRenderer)
            {
                // Reset previous highlight
                ResetHighlight();

                // Set new highlight
                currentRenderer = hitRenderer;
                originalColor = currentRenderer.material.color;
                currentRenderer.material.color = Color.yellow;

                // Set highlighted object reference
                highlightedObject = hitRenderer.gameObject;
                lastHighlightedObject = hitRenderer.gameObject; // NEW: keep last highlighted
            }
        }
        else
        {
            // Log miss
            Debug.Log("Ray missed all objects");
            ResetHighlight();
        }

        // Update LineRenderer positions
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void ResetHighlight()
    {
        if (currentRenderer != null)
        {
            currentRenderer.material.color = originalColor;
            currentRenderer = null;
            highlightedObject = null; // keep lastHighlightedObject intact
        }
    }
}
