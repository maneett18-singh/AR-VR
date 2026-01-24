using UnityEngine;

public class Socket : MonoBehaviour
{
    public int socketIndex;
    public Transform snapPoint; // Empty GameObject slightly in front of socket
    public Renderer statusRenderer; 

    [HideInInspector] public Wire currentWire;

    [HideInInspector] public bool isOccupied = false;

    public void TryPlug(Wire wire) 
    {
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
            statusRenderer.material.color = Color.green;
            wire.isLocked = true; // Correct wire: locked forever
        } else {
            statusRenderer.material.color = Color.red;
            wire.isLocked = false; // Wrong wire: remains grabbable
        }
    }

    public void ResetSocket() 
    {
        isOccupied = false;
        currentWire = null;
        statusRenderer.material.color = Color.white;
    }
}