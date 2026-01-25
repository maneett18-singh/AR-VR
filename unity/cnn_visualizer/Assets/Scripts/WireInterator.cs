using UnityEngine;

public class WireInteractor : MonoBehaviour
{
    public float reach = 5f;
    public Transform holdPoint; // Child of Camera, Scale (1,1,1)
    private Wire heldWire;

    void Update() 
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, reach)) 
        {
            // 1. TRY TO PLUG IN (If already holding something)
            if (heldWire != null) 
            {
                Socket socket = hit.collider.GetComponent<Socket>();
                if (socket != null && !socket.isOccupied) 
                {
                    socket.TryPlug(heldWire);
                    heldWire = null; // Clear hand reference
                    return;
                }
            } 

            // 2. TRY TO PICK UP (If hands are empty)
            if (heldWire == null) 
            {
                PlugIdentity id = hit.collider.GetComponentInParent<PlugIdentity>();
                if (id != null && !id.wireRoot.isLocked) 
                {
                    heldWire = id.wireRoot;
                    heldWire.PickUp(holdPoint);
                    Debug.Log("Picked up wire via Identity Badge");
                }
            }
        }
    }
}