using UnityEngine;

/// <summary>
/// Ensures a RaycastVisibleOnKey exists in the scene for XR interactions.
/// Attach this to the XR Origin. It will create a child GameObject with the raycast script if missing.
/// </summary>
public class RaycastAutoSetup : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Optional existing RaycastVisibleOnKey in scene. If null, one will be created.")]
    public RaycastVisibleOnKey raycastPrefab;

    [Tooltip("Optional hold point (e.g., Right Controller). If null, will attempt to find one.")]
    public Transform holdPoint;

    [Tooltip("Optional player camera. If null, will use Camera.main.")]
    public Camera playerCamera;

    [Tooltip("Child name to search for hold point if not assigned.")]
    public string holdPointName = "Right Controller";

    private void Awake()
    {
        EnsureRaycastExists();
    }

    private void EnsureRaycastExists()
    {
        RaycastVisibleOnKey existing = FindObjectOfType<RaycastVisibleOnKey>();
        if (existing != null)
        {
            ConfigureRaycast(existing.gameObject, existing);
            return;
        }

        GameObject rayObj = new GameObject("XR Raycast");
        rayObj.transform.SetParent(transform, false);
        var raycast = rayObj.AddComponent<RaycastVisibleOnKey>();
        ConfigureRaycast(rayObj, raycast);
    }

    private void ConfigureRaycast(GameObject rayObj, RaycastVisibleOnKey raycast)
    {
        if (raycast == null)
            return;

        var lineRenderer = rayObj.GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = rayObj.AddComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.enabled = false;
        if (lineRenderer.material == null)
            lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = Color.orange;

        raycast.player = transform;
        raycast.playerCamera = playerCamera != null ? playerCamera : Camera.main;
        raycast.holdPoint = holdPoint != null ? holdPoint : FindHoldPoint();
        raycast.allowKeyboardInput = false;
    raycast.enableHighlightInput = false;
    }

    private Transform FindHoldPoint()
    {
        if (holdPoint != null)
            return holdPoint;

        if (!string.IsNullOrEmpty(holdPointName))
        {
            var t = transform.Find(holdPointName);
            if (t != null)
                return t;

            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == holdPointName)
                    return child;
            }
        }

        return transform;
    }
}