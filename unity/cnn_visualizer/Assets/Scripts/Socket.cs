using UnityEngine;

public class Socket : MonoBehaviour
{
    public int socketIndex;
    public Transform snapPoint; // Empty GameObject slightly in front of socket
    public Renderer statusRenderer; 

    [Header("Auto Snap")]
    public bool autoSnapEnabled = true;
    [Tooltip("Distance from snapPoint within which a plug will auto-attach.")]
    public float autoSnapDistance = 0.2f;
    [Tooltip("Optional: restrict auto-snap detection to specific layers. Default is Everything.")]
    public LayerMask autoSnapLayers = ~0;

    [HideInInspector] public Wire currentWire;

    [HideInInspector] public bool isOccupied = false;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock _mpb;

    private readonly Collider[] _autoSnapHits = new Collider[16];

    private void Awake()
    {
        // If not assigned in Inspector, try to find a visible renderer on this object or children.
        if (statusRenderer == null)
            statusRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();

        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (!autoSnapEnabled || snapPoint == null)
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(
            snapPoint.position,
            autoSnapDistance,
            _autoSnapHits,
            autoSnapLayers,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _autoSnapHits[i];
            if (hit == null)
                continue;

            PlugIdentity plugId = hit.GetComponent<PlugIdentity>();
            if (plugId == null)
                plugId = hit.GetComponentInParent<PlugIdentity>();

            if (plugId == null || plugId.wireRoot == null)
                continue;

            Wire wire = plugId.wireRoot;

            // Don't fight with locked wires.
            if (wire.isLocked)
                continue;

            // If this exact wire is already plugged into this socket, do nothing.
            if (isOccupied && currentWire == wire)
                return;

            TryPlug(wire);
            return;
        }
    }

    private void SetStatusColor(Color color)
    {
        if (statusRenderer == null)
            return;

        statusRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(ColorId, color);
        _mpb.SetColor(BaseColorId, color);
        statusRenderer.SetPropertyBlock(_mpb);
    }

    public void TryPlug(Wire wire) 
    {
        if (wire == null)
            return;

        // Already plugged here.
        if (isOccupied && currentWire == wire)
            return;

        // If something is already plugged in:
        // - If it's correct/locked, do not allow replacing.
        // - If it's wrong/unlocked, allow overriding by unplugging it first.
        if (isOccupied)
        {
            if (currentWire != null && currentWire.isLocked)
                return;

            if (currentWire != null)
                currentWire.UnplugFromSocket();

            ResetSocket();
        }

        isOccupied = true;
        currentWire = wire;
        wire.currentSocket = this;
        wire.SnapToSocket(snapPoint);

        // Success Check
        if (wire.wireIndex == socketIndex) {
            SetStatusColor(Color.green);
            wire.isLocked = true; // Correct wire: locked forever
        } else {
            SetStatusColor(Color.red);
            wire.isLocked = false; // Wrong wire: remains grabbable
        }
    }

    public void ResetSocket() 
    {
        isOccupied = false;
        currentWire = null;
        SetStatusColor(Color.white);
    }
}