using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

[RequireComponent(typeof(LineRenderer))]
public class ZiplineController : MonoBehaviour
{
    [Header("References")]
    public Transform startPoint;
    public Transform endPoint;
    public Transform player;                   // assign your player in Inspector
    public Animator playerAnimator;            // optional: zipline ride animation

    [Header("XR Interaction (Optional)")]
    [Tooltip("Assign an XR Base Interactable attached to the zipline trigger/collider.")]
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    [Tooltip("Optional: assign your XR rig root. If null, the interactor root is used.")]
    public Transform playerRootOverride;

    [Header("Settings")]
    public float speed = 5f;                   // zipline speed
    public bool drawDebugLine = false;         // disable this if you have a real rope mesh
    public int ropeSegments = 20;              // segments for smooth rope
    public float ropeSag = 0.5f;               // rope sag amount

    private bool isUsingZipline = false;
    private float t = 0f;
    private LineRenderer line;

    private void OnEnable()
    {
        if (interactable == null)
            interactable = GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (interactable != null)
            interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

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

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args == null) return;
        if (!IsRightHandInteractor(args.interactorObject)) return;

        Transform target = ResolvePlayerRoot(args.interactorObject);
        Attach(target);
    }

    private static bool IsRightHandInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
    {
        if (interactor == null) return false;

        var xrController = interactor.transform.GetComponentInParent<XRController>();
        if (xrController != null)
            return xrController.controllerNode == XRNode.RightHand;

        return true;
    }

    private Transform ResolvePlayerRoot(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
    {
        if (playerRootOverride != null)
            return playerRootOverride;

        var xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null)
            return xrOrigin.transform;

        if (Camera.main != null)
            return Camera.main.transform.root;

        return interactor != null ? interactor.transform.root : null;
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
