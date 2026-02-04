using UnityEngine;

public class RaycastPlugAndSocket : MonoBehaviour
{
    public float rayDistance = 5f;
    public Transform holdPoint;
    private Wire heldWire;

    void Update()
    {
        // If the wire got auto-snapped into a socket, release it from the "hand".
        if (heldWire != null && heldWire.currentSocket != null)
            heldWire = null;

        if (Input.GetKeyDown(KeyCode.E))
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, rayDistance))
            {
                HandleHit(hit.collider);
            }
        }
    }

    void HandleHit(Collider col)
    {
        // Case A: Picking up a wire
        if (col.CompareTag("WirePlug") && heldWire == null)
        {
            Wire wire = col.GetComponentInParent<Wire>();
            if (wire != null && !wire.isLocked)
            {
                heldWire = wire;
                heldWire.PickUp(holdPoint); // Moves only the plug
            }
        }
        // Case B: Plugging into a socket
        else if (col.CompareTag("Socket") && heldWire != null)
        {
            Socket socket = col.GetComponent<Socket>();
            if (socket != null)
            {
                socket.TryPlug(heldWire);
                heldWire = null; // Clear reference so we can pick up another
            }
        }
    }
}