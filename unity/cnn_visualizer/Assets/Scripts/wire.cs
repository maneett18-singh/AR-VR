using UnityEngine;
using System.Collections.Generic;

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
    private bool originalPlugIsTrigger;
    private Vector3 originalPlugWorldScale;
    private Transform followTarget;

    // If you're using XR Interaction Toolkit, these components can keep driving the plug's
    // transform even after we snap it, causing "floating" or scale/rotation glitches.
    private Behaviour[] xrBehaviours;
    private bool[] xrBehavioursWereEnabled;

    void Awake() 
    {
        plugRb = plug.GetComponent<Rigidbody>();
        plugCol = plug.GetComponent<Collider>();
        line = GetComponent<LineRenderer>();

        originalPlugWorldScale = plug.lossyScale;

        if (plugCol != null)
            originalPlugIsTrigger = plugCol.isTrigger;

        CacheXrBehaviours();

        // Line setup
        line.positionCount = 2;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;

        // Attach the ID badge so Raycast can find this script from the plug
        if (plug.GetComponent<PlugIdentity>() == null) {
            plug.gameObject.AddComponent<PlugIdentity>().wireRoot = this;
        }
    }

    private void LateUpdate()
    {
        // Follow without parenting to scaled/rotated hierarchies (prevents shearing/flattening).
        if (followTarget != null && plug != null)
        {
            plug.position = followTarget.position;
            plug.rotation = followTarget.rotation;
            SetPlugWorldScale(originalPlugWorldScale);
        }

        // Visual cable connection
        if (startPoint != null && plug != null)
        {
            line.SetPosition(0, startPoint.position);
            line.SetPosition(1, plug.position);
        }
    }

    private void SetPlugWorldScale(Vector3 desiredWorldScale)
    {
        if (plug == null)
            return;

        Transform parent = plug.parent;
        if (parent == null)
        {
            plug.localScale = desiredWorldScale;
            return;
        }

        Vector3 parentLossy = parent.lossyScale;
        const float eps = 1e-6f;

        float sx = Mathf.Abs(parentLossy.x) < eps ? eps : parentLossy.x;
        float sy = Mathf.Abs(parentLossy.y) < eps ? eps : parentLossy.y;
        float sz = Mathf.Abs(parentLossy.z) < eps ? eps : parentLossy.z;

        plug.localScale = new Vector3(
            desiredWorldScale.x / sx,
            desiredWorldScale.y / sy,
            desiredWorldScale.z / sz
        );
    }

    private void CacheXrBehaviours()
    {
        if (plug == null)
            return;

        // Collect XRGrabInteractable + any *GrabTransformer behaviours by type name,
        // without taking a hard dependency on the XR Interaction Toolkit assembly.
        Behaviour[] behaviours = plug.GetComponents<Behaviour>();
        List<Behaviour> list = new List<Behaviour>();
        List<bool> enabledStates = new List<bool>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];
            if (b == null)
                continue;

            string typeName = b.GetType().Name;
            if (typeName == "XRGrabInteractable" || typeName.Contains("GrabTransformer"))
            {
                list.Add(b);
                enabledStates.Add(b.enabled);
            }
        }

        xrBehaviours = list.Count > 0 ? list.ToArray() : null;
        xrBehavioursWereEnabled = enabledStates.Count > 0 ? enabledStates.ToArray() : null;
    }

    private void SetXrBehavioursEnabled(bool enabled, bool restoreOriginalStates = false)
    {
        if (xrBehaviours == null || xrBehaviours.Length == 0)
            return;

        for (int i = 0; i < xrBehaviours.Length; i++)
        {
            Behaviour b = xrBehaviours[i];
            if (b == null)
                continue;

            if (restoreOriginalStates && xrBehavioursWereEnabled != null && i < xrBehavioursWereEnabled.Length)
                b.enabled = xrBehavioursWereEnabled[i];
            else
                b.enabled = enabled;
        }
    }

    void Update() { }

    public void PickUp(Transform holdPoint) 
    {
        if (isLocked) return;

        // Ensure XR isn't still controlling the plug while we move it by script.
        SetXrBehavioursEnabled(false);

        // If pulling out of a socket, tell the socket it's free now
        if (currentSocket != null) {
            currentSocket.ResetSocket();
            currentSocket = null;
        }

        if (plugRb != null) plugRb.isKinematic = true;
        // Keep collider enabled so sockets can auto-snap by proximity,
        // but make it a trigger so it doesn't physically collide while carried.
        if (plugCol != null)
        {
            plugCol.enabled = true;
            plugCol.isTrigger = true;
        }

        // Keep plug under the wire root and follow the hand point in world space.
        plug.SetParent(transform, true);
        followTarget = holdPoint;
    }

    public void SnapToSocket(Transform snapPoint) 
    {
        // Stop XR from overriding position/rotation/scale after we snap.
        SetXrBehavioursEnabled(false);

        // Keep plug under the wire root and follow the socket point in world space.
        plug.SetParent(transform, true);
        followTarget = snapPoint;

        if (plugRb != null)
        {
            plugRb.isKinematic = true;
            plugRb.linearVelocity = Vector3.zero;
            plugRb.angularVelocity = Vector3.zero;
        }
        
        // IMPORTANT: Re-enable collider so we can grab it again if it's wrong!
        if (plugCol != null)
        {
            plugCol.enabled = true;
            plugCol.isTrigger = originalPlugIsTrigger;
        }
    }

    public void UnplugFromSocket()
    {
        followTarget = null;

        // Detach plug from socket so another wire can be plugged in.
        // Keep world position so it doesn't teleport.
        if (plug != null)
            plug.SetParent(transform, true);

        // Make it grabbable again.
        if (plugRb != null)
            plugRb.isKinematic = false;
        if (plugCol != null)
        {
            plugCol.enabled = true;
            plugCol.isTrigger = originalPlugIsTrigger;
        }

        // Restore XR behaviour states so the plug can be grabbed normally again.
        SetXrBehavioursEnabled(true, restoreOriginalStates: true);

        currentSocket = null;
    }
}

// THE HELPER CLASS (Put this outside the Wire class brackets)
public class PlugIdentity : MonoBehaviour 
{ 
    public Wire wireRoot; 
}