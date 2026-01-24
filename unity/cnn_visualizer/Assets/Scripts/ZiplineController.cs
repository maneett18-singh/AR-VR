using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class ZiplineController : MonoBehaviour
{
    [Header("References")]
    public Transform startPoint;
    public Transform endPoint;
    public Transform player;                   // assign your player in Inspector
    public Animator playerAnimator;            // optional: zipline ride animation

    [Header("Settings")]
    public float speed = 5f;                   // zipline speed
    public bool drawDebugLine = false;         // disable this if you have a real rope mesh
    public int ropeSegments = 20;              // segments for smooth rope
    public float ropeSag = 0.5f;               // rope sag amount

    private bool isUsingZipline = false;
    private float t = 0f;
    private LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (line != null)
        {
            line.enabled = drawDebugLine;
            line.positionCount = Mathf.Max(2, ropeSegments);
        }
    }

    void Update()
    {
        if (drawDebugLine)
            UpdateRope();

        if (!isUsingZipline || player == null) return;

        // Move player along the zipline
        t += Time.deltaTime * speed / Vector3.Distance(startPoint.position, endPoint.position);
        player.position = Vector3.Lerp(startPoint.position, endPoint.position, t);

        // Rotate player to face forward along the rope
        Vector3 forwardDir = (endPoint.position - startPoint.position).normalized;
        player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(forwardDir), Time.deltaTime * 5f);

        // Optional: play zipline animation
        if (playerAnimator != null)
            playerAnimator.SetBool("isRidingZipline", true);

        // Stop zipline at the end
        if (t >= 1f)
            Detach();
    }

    // Call this to attach the player to the zipline
    public void Attach(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            Debug.LogError("Player reference is null!");
            return;
        }

        player = playerTransform;
        t = 0f;
        isUsingZipline = true;

        // Disable CharacterController during zipline
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        // Optional: animator handling
        if (playerAnimator != null)
            playerAnimator.SetBool("isRidingZipline", true);
    }

    private void Detach()
    {
        isUsingZipline = false;

        // Re-enable CharacterController
        if (player != null)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = true;

            if (playerAnimator != null)
                playerAnimator.SetBool("isRidingZipline", false);
        }

        player = null;
    }

    private void UpdateRope()
    {
        if (!drawDebugLine) return;
        if (line == null || startPoint == null || endPoint == null) return;

        if (!line.enabled) line.enabled = true;
        line.positionCount = Mathf.Max(2, ropeSegments);

        for (int i = 0; i < ropeSegments; i++)
        {
            float tSegment = i / (float)(ropeSegments - 1);
            Vector3 pos = Vector3.Lerp(startPoint.position, endPoint.position, tSegment);

            // Add sag: sine curve for smooth realistic rope
            pos.y -= Mathf.Sin(tSegment * Mathf.PI) * ropeSag;

            line.SetPosition(i, pos);
        }
    }
}
