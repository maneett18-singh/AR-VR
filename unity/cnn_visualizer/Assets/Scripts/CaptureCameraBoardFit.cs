using UnityEngine;

public class CaptureCameraBoardFit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera captureCamera;
    [SerializeField] private Renderer boardRenderer;
    [SerializeField] private Collider boardCollider;

    [Header("Capture Settings")]
    [SerializeField] private bool useOrthographic = true;
    [SerializeField] private float distanceFromBoard = 0.25f;
    [SerializeField] private float padding = 0.02f;
    [SerializeField] private Color backgroundColor = Color.white;
    [SerializeField] private LayerMask boardLayerOverride = 0;
    [Tooltip("Optional extra layers to include (e.g., stroke/ink layer).")]
    [SerializeField] private LayerMask additionalLayers = 0;

    [Header("Apply")]
    [SerializeField] private bool applyOnEnable = true;

    private void OnEnable()
    {
        if (applyOnEnable)
            Apply();
    }

    [ContextMenu("Apply")]
    public void Apply()
    {
        var cam = captureCamera != null ? captureCamera : GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("CaptureCameraBoardFit: No camera found.");
            return;
        }

        var bounds = GetBoardBounds();
        if (!bounds.HasValue)
        {
            Debug.LogWarning("CaptureCameraBoardFit: No board renderer/collider assigned.");
            return;
        }

        // Align camera to face the board front.
        Transform boardTransform = boardRenderer != null ? boardRenderer.transform : boardCollider.transform;
        cam.transform.rotation = Quaternion.LookRotation(-boardTransform.forward, boardTransform.up);
        cam.transform.position = bounds.Value.center - cam.transform.forward * Mathf.Max(0.01f, distanceFromBoard);

        // Render only the board layer.
    int boardLayer = boardTransform.gameObject.layer;
    int boardMask = boardLayerOverride != 0 ? boardLayerOverride.value : (1 << boardLayer);
    int extraMask = additionalLayers != 0 ? additionalLayers.value : 0;
    cam.cullingMask = boardMask | extraMask;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;

        if (useOrthographic)
        {
            cam.orthographic = true;
            FitOrthographic(cam, bounds.Value, padding);
        }
    }

    private Bounds? GetBoardBounds()
    {
        if (boardRenderer != null)
            return boardRenderer.bounds;
        if (boardCollider != null)
            return boardCollider.bounds;
        return null;
    }

    private static void FitOrthographic(Camera cam, Bounds bounds, float padding)
    {
        Vector3[] corners = new Vector3[8];
        Vector3 c = bounds.center;
        Vector3 e = bounds.extents;

        corners[0] = c + new Vector3(-e.x, -e.y, -e.z);
        corners[1] = c + new Vector3(e.x, -e.y, -e.z);
        corners[2] = c + new Vector3(-e.x, e.y, -e.z);
        corners[3] = c + new Vector3(e.x, e.y, -e.z);
        corners[4] = c + new Vector3(-e.x, -e.y, e.z);
        corners[5] = c + new Vector3(e.x, -e.y, e.z);
        corners[6] = c + new Vector3(-e.x, e.y, e.z);
        corners[7] = c + new Vector3(e.x, e.y, e.z);

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float maxZ = 0f;

        foreach (var corner in corners)
        {
            Vector3 local = cam.transform.InverseTransformPoint(corner);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
            maxZ = Mathf.Max(maxZ, local.z);
        }

        float halfWidth = Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX));
        float halfHeight = Mathf.Max(Mathf.Abs(minY), Mathf.Abs(maxY));

        cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.0001f, cam.aspect)) + padding;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = Mathf.Max(1f, maxZ + 1f);
    }
}
