using UnityEngine;
using System.Collections.Generic;

public class NetworkManagerHider : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Network Manager object to hide when connections are correct.")]
    public GameObject networkManagerObj;

    [Tooltip("The specific indices required to trigger the hide action.")]
    public int[] requiredIndices = { 0, 1, 2, 4 };

    [Header("Socket Tracking")]
    [Tooltip("If true, searches the scene for sockets matching the indices above.")]
    public bool autoFindSockets = true;

    [Tooltip("Optional root to limit socket search (recommended). If null, searches the whole scene.")]
    public Transform socketsRoot;

    public List<Socket> targetSockets = new List<Socket>();

    [Header("Visibility")]
    [Tooltip("If true, the target is hidden UNTIL all required sockets are connected correctly (i.e., visible only when all connections are correct).")]
    public bool visibleOnlyWhenAllConnected = false;

    [Tooltip("If true (default), once the target is hidden after success, it stays hidden forever.")]
    public bool stickyHideAfterSuccess = true;

    [Tooltip("If true, logs missing sockets and connection status details.")]
    public bool verboseLogs = false;

    private bool _isDeactivated = false;
    private float _nextAutoRefreshTime = 0f;
    private const float AutoRefreshIntervalSeconds = 1f;

    private void Start()
    {
        if (autoFindSockets)
            FindSpecificSockets();

        ApplyVisibility(force: true);
    }

    private void FindSpecificSockets()
    {
        targetSockets.Clear();

        Socket[] allSockets = socketsRoot != null
            ? socketsRoot.GetComponentsInChildren<Socket>(includeInactive: true)
            : FindObjectsOfType<Socket>(includeInactive: true);

        foreach (int index in requiredIndices)
        {
            Socket found = null;
            foreach (var s in allSockets)
            {
                if (s.socketIndex == index)
                {
                    found = s;
                    break;
                }
            }

            if (found != null)
            {
                targetSockets.Add(found);
            }
            else if (verboseLogs)
            {
                Debug.LogWarning($"[NetworkManagerHider] Could not find Socket with socketIndex={index}. " +
                                 $"(SearchRoot={(socketsRoot != null ? socketsRoot.name : "<scene>")})");
            }
        }

        if (verboseLogs)
            Debug.Log($"[NetworkManagerHider] Tracking {targetSockets.Count}/{requiredIndices.Length} sockets.");
    }

    private void Update()
    {
        if (autoFindSockets && (targetSockets.Count < requiredIndices.Length || targetSockets.Contains(null)))
        {
            if (Time.unscaledTime >= _nextAutoRefreshTime)
            {
                _nextAutoRefreshTime = Time.unscaledTime + AutoRefreshIntervalSeconds;
                FindSpecificSockets();
            }
        }

        ApplyVisibility(force: false);
    }

    private void ApplyVisibility(bool force)
    {
        if (networkManagerObj == null)
            return;

        bool allConnected = CheckAllConnections();

        if (visibleOnlyWhenAllConnected)
        {
            // With this mode, it stays hidden until the puzzle is fully correct.
            // NOTE: If this script is on the same object you're toggling, it cannot re-enable itself.
            if (networkManagerObj == gameObject && !allConnected)
            {
                if (verboseLogs)
                    Debug.LogWarning("[NetworkManagerHider] visibleOnlyWhenAllConnected is enabled, but networkManagerObj is the same GameObject as this script. Put this script on a different GameObject to allow toggling.");
                return;
            }

            if (force || networkManagerObj.activeSelf != allConnected)
                networkManagerObj.SetActive(allConnected);

            return;
        }

        // Default behavior: object is visible until all connections are correct, then it hides.
        if (_isDeactivated && stickyHideAfterSuccess)
            return;

        if (allConnected)
        {
            HideTarget();
        }
        else if (!stickyHideAfterSuccess)
        {
            // Optional: allow reappearing if a wire is removed.
            if (networkManagerObj == gameObject)
            {
                if (verboseLogs)
                    Debug.LogWarning("[NetworkManagerHider] stickyHideAfterSuccess is disabled, but networkManagerObj is the same GameObject as this script. Put this script on a different GameObject to allow toggling.");
                return;
            }

            if (force || !networkManagerObj.activeSelf)
                networkManagerObj.SetActive(true);
        }
    }

    private bool CheckAllConnections()
    {
        // Safety check: ensure we actually found the sockets we need
        if (targetSockets.Count < requiredIndices.Length)
            return false;

        foreach (Socket s in targetSockets)
        {
            if (s == null) return false;

            // Logic based on your Socket.cs: 
            // A connection is only 'correct' if it is occupied and locked.
            if (!s.isOccupied || s.currentWire == null || !s.currentWire.isLocked)
                return false;
        }

        return true;
    }

    private void HideTarget()
    {
        if (networkManagerObj != null)
        {
            networkManagerObj.SetActive(false);
            _isDeactivated = true;
            Debug.Log("<color=green>Network Manager Hidden: All required sockets connected correctly!</color>");
        }
    }
}