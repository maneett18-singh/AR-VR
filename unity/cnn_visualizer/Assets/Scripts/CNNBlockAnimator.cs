using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
public class CNNBlockAnimator : MonoBehaviour
{
    public Transform filter;
    public Transform convPlane;
    public Transform bnPlane;
    public Transform reluPlane;
    public Transform poolPlane;

    public float slideSpeed = 0.5f;
    public float delayBetweenStages = 1f;
    public float moveSpeed = 1.0f; // new: speed used for smooth movement across input
    public int gridCols = 8;       // new: horizontal sample count across the plane
    public int gridRows = 8;       // new: vertical sample count down the plane

    private Vector3[] filterPositions;
    private bool isAnimating = false;

    void Start()
    {
        // Define a small grid of filter positions (3x3 sample)
        filterPositions = new Vector3[]
        {
            new Vector3(-0.03f, 0.03f, -0.01f),
            new Vector3(0f, 0.03f, -0.01f),
            new Vector3(0.03f, 0.03f, -0.01f),

            new Vector3(-0.03f, 0f, -0.01f),
            new Vector3(0f, 0f, -0.01f),
            new Vector3(0.03f, 0f, -0.01f),

            new Vector3(-0.03f, -0.03f, -0.01f),
            new Vector3(0f, -0.03f, -0.01f),
            new Vector3(0.03f, -0.03f, -0.01f)
        };

        // start at top-left
        if (filterPositions.Length > 0 && filter != null)
        {
            filter.localPosition = filterPositions[0];
        }

        // start automatic sliding loop (top-left to bottom-right, repeating)
        StartCoroutine(SlideFilterLoop());
    }

    void Update()
    {
        // no manual trigger; sliding is automatic
    }

    IEnumerator AnimateCNNBlock()
    {
        isAnimating = true;

        // Step 1: Slide filter across input is now handled continuously by SlideFilterLoop.
        yield return new WaitForSeconds(delayBetweenStages);

        // Step 2: Highlight ConvPlane
        FlashPlane(convPlane, Color.cyan);
        yield return new WaitForSeconds(delayBetweenStages);

        // Step 3: Highlight BNPlane
        FlashPlane(bnPlane, Color.yellow);
        yield return new WaitForSeconds(delayBetweenStages);

        // Step 4: Highlight ReLUPlane
        FlashPlane(reluPlane, Color.green);
        yield return new WaitForSeconds(delayBetweenStages);

        // Step 5: Highlight PoolPlane
        FlashPlane(poolPlane, new Color(0.6f, 0f, 1f));
        yield return new WaitForSeconds(delayBetweenStages);

        isAnimating = false;
    }

    void FlashPlane(Transform plane, Color color)
    {
        var rend = plane.GetComponent<Renderer>();
        if (rend)
        {
            rend.material.color = color;
        }
    }

    // New: raster-scan across convPlane from top-left to bottom-right and loop.
    IEnumerator SlideFilterLoop()
    {
        if (filter == null || convPlane == null)
            yield break;

        var mf = convPlane.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            yield break;

        // Local-space bounds of the mesh (orientation-agnostic)
        Bounds lb = mf.sharedMesh.bounds;
        Vector3 c = lb.center;
        Vector3 e = lb.extents;

        // Determine which axis is thin (normal), and which two span the surface
        const float EPS = 1e-5f;
        int thin = (e.x < EPS) ? 0 : (e.y < EPS ? 1 : 2);
        int axA, axB; // surface axes: axA = left-right, axB = top-bottom
        if (thin == 0) { axA = 1; axB = 2; }        // surface is YZ
        else if (thin == 1) { axA = 0; axB = 2; }   // surface is XZ
        else { axA = 0; axB = 1; }                  // surface is XY (Quad)

        // Compute local-space corners on the surface
        float left   = (axA == 0 ? c.x - e.x : (axA == 1 ? c.y - e.y : c.z - e.z));
        float right  = (axA == 0 ? c.x + e.x : (axA == 1 ? c.y + e.y : c.z + e.z));
        float top    = (axB == 0 ? c.x + e.x : (axB == 1 ? c.y + e.y : c.z + e.z));
        float bottom = (axB == 0 ? c.x - e.x : (axB == 1 ? c.y - e.y : c.z - e.z));

        int cols = Mathf.Max(2, gridCols);
        int rows = Mathf.Max(2, gridRows);
        float stepA = (right - left) / (cols - 1);
        float stepB = (top - bottom) / (rows - 1);

        // Preserve current offset from plane along its normal so filter stays above it
        Vector3 planeNormal = (thin == 0) ? convPlane.right : (thin == 1 ? convPlane.up : convPlane.forward);
        float normalOffset = Vector3.Dot(filter.position - convPlane.position, planeNormal);

        while (true)
        {
            for (int r = 0; r < rows; r++)
            {
                // Flip the vertical traversal so visual path becomes top -> bottom
                float coordB = bottom + r * stepB;

                // Sweep left -> right visually by flipping the horizontal traversal
                for (int cIdx = 0; cIdx < cols; cIdx++)
                {
                    float coordA = right - cIdx * stepA;

                    // Build local target on plane surface
                    Vector3 local = c;
                    if (axA == 0) local.x = coordA; else if (axA == 1) local.y = coordA; else local.z = coordA;
                    if (axB == 0) local.x = coordB; else if (axB == 1) local.y = coordB; else local.z = coordB;

                    // Convert to world and add preserved normal offset
                    Vector3 worldTarget = convPlane.TransformPoint(local) + planeNormal * normalOffset;

                    // Smooth move to target
                    while (Vector3.Distance(filter.position, worldTarget) > 0.0005f)
                    {
                        filter.position = Vector3.MoveTowards(filter.position, worldTarget, moveSpeed * Time.deltaTime);
                        yield return null;
                    }

                    // brief pause per cell
                    yield return new WaitForSeconds(0.08f);
                }

                // Start next row at the right edge, then sweep to the left
                if (r < rows - 1)
                {
                    float nextB = bottom + (r + 1) * stepB;
                    Vector3 localStart = c;
                    if (axA == 0) localStart.x = right; else if (axA == 1) localStart.y = right; else localStart.z = right;
                    if (axB == 0) localStart.x = nextB; else if (axB == 1) localStart.y = nextB; else localStart.z = nextB;

                    Vector3 startWorld = convPlane.TransformPoint(localStart) + planeNormal * normalOffset;
                    filter.position = startWorld;
                    yield return null;
                }
            }
        }
    }

}
