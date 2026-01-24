using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Wire : MonoBehaviour
{
    public int wireIndex;

    [Header("Wire Components")]
    public Transform startPoint;   // Fixed anchor on the server
    public Transform plug;         // The movable tip

    [HideInInspector] public bool isLocked = false;
    [HideInInspector] public Socket currentSocket; 
    
    private Rigidbody plugRb;
    private Collider plugCol;
    private LineRenderer line;
    private Vector3 originalPlugScale;

    void Awake() 
    {
        plugRb = plug.GetComponent<Rigidbody>();
        plugCol = plug.GetComponent<Collider>();
        line = GetComponent<LineRenderer>();
        
        // Store original scale
        originalPlugScale = plug.lossyScale;

        // Line setup
        line.positionCount = 2;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;

        // Attach the ID badge so Raycast can find this script from the plug
        if (plug.GetComponent<PlugIdentity>() == null) {
            plug.gameObject.AddComponent<PlugIdentity>().wireRoot = this;
        }
    }

    void Update()
    {
        // Visual cable connection
        if (startPoint != null && plug != null)
        {
            line.SetPosition(0, startPoint.position);
            line.SetPosition(1, plug.position);
        }
    }

    public void PickUp(Transform holdPoint) 
    {
        if (isLocked) return;

        // If pulling out of a socket, tell the socket it's free now
        if (currentSocket != null) {
            currentSocket.ResetSocket();
            currentSocket = null;
        }

        if (plugRb != null) plugRb.isKinematic = true;
        if (plugCol != null) plugCol.enabled = false; // Disable while carrying

        plug.SetParent(holdPoint);
        plug.localPosition = Vector3.zero;
        plug.localRotation = Quaternion.identity;
        
        // Preserve original world scale
        plug.localScale = new Vector3(
            originalPlugScale.x / holdPoint.lossyScale.x,
            originalPlugScale.y / holdPoint.lossyScale.y,
            originalPlugScale.z / holdPoint.lossyScale.z
        );
    }

    public void SnapToSocket(Transform snapPoint) 
    {
        plug.SetParent(snapPoint);
        plug.localPosition = Vector3.zero;
        plug.localRotation = Quaternion.identity;
        
        // Preserve original world scale
        plug.localScale = new Vector3(
            originalPlugScale.x / snapPoint.lossyScale.x,
            originalPlugScale.y / snapPoint.lossyScale.y,
            originalPlugScale.z / snapPoint.lossyScale.z
        );

        if (plugRb != null) plugRb.isKinematic = true;
        
        // IMPORTANT: Re-enable collider so we can grab it again if it's wrong!
        if (plugCol != null) plugCol.enabled = true; 
    }

    public void UnplugFromSocket()
    {
        // Detach plug from socket so another wire can be plugged in.
        // Keep world position so it doesn't teleport.
        if (plug != null)
            plug.SetParent(transform, true);

        // Make it grabbable again.
        if (plugRb != null)
            plugRb.isKinematic = false;
        if (plugCol != null)
            plugCol.enabled = true;

        currentSocket = null;
    }
}

// THE HELPER CLASS (Put this outside the Wire class brackets)
public class PlugIdentity : MonoBehaviour 
{ 
    public Wire wireRoot; 
}