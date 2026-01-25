using UnityEngine;

public class FCRevealOnCorrectSocket : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("FC root object to show/hide.")]
    public GameObject fcRoot;

    [Tooltip("Socket index required to reveal FC.")]
    public int requiredSocketIndex = 3;

    [Header("Auto Find (Optional)")]
    [Tooltip("If true and fcRoot is not assigned, this will try to find a GameObject named 'FC'.")]
    public bool autoFindFcByName = true;

    [Tooltip("If true, this will search the scene for a Socket with socketIndex == requiredSocketIndex.")]
    public bool autoFindSocketByIndex = true;

    [Tooltip("Optional explicit socket reference (recommended).")]
    public Socket targetSocket;

    private bool _lastVisible;

    private void Awake()
    {
        if (fcRoot == null && autoFindFcByName)
            fcRoot = GameObject.Find("FC");

        if (targetSocket == null && autoFindSocketByIndex)
        {
            foreach (var socket in FindObjectsOfType<Socket>(includeInactive: true))
            {
                if (socket != null && socket.socketIndex == requiredSocketIndex)
                {
                    targetSocket = socket;
                    break;
                }
            }
        }

        // Default to hidden until the correct connection exists.
        SetVisible(IsCorrectConnection());
    }

    private void Update()
    {
        bool visible = IsCorrectConnection();
        if (visible == _lastVisible)
            return;

        SetVisible(visible);
    }

    private bool IsCorrectConnection()
    {
        if (targetSocket == null)
            return false;

        if (targetSocket.socketIndex != requiredSocketIndex)
            return false;

        if (!targetSocket.isOccupied)
            return false;

        if (targetSocket.currentWire == null)
            return false;

        // Correct connection in your logic locks the wire.
        if (!targetSocket.currentWire.isLocked)
            return false;

        return targetSocket.currentWire.wireIndex == requiredSocketIndex;
    }

    private void SetVisible(bool visible)
    {
        _lastVisible = visible;
        if (fcRoot != null)
            fcRoot.SetActive(visible);
    }
}
