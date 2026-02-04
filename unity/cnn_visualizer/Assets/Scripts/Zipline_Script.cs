using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public class Zipline : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    public Transform player; // assign your player object in inspector

    [Header("XR Interaction (Optional)")]
    [Tooltip("Assign an XR Base Interactable attached to the zipline trigger/collider.")]
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    [Tooltip("Optional: assign your XR rig root. If null, the interactor root is used.")]
    public Transform playerRootOverride;

    public float speed = 5f;

    private bool isUsingZipline;
    private float t;

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

    void Update()
    {
        if (!isUsingZipline || player == null) return;

        t += Time.deltaTime * speed / Vector3.Distance(startPoint.position, endPoint.position);
        player.position = Vector3.Lerp(startPoint.position, endPoint.position, t);

        if (t >= 1f)
        {
            Detach();
        }
    }

    public void Attach(Transform playerTransform)
{
    Debug.Log("Player attached: " + playerTransform.name);
    player = playerTransform;
    t = 0f;
    isUsingZipline = true;

    var controller = player.GetComponent<CharacterController>();
    if (controller != null) controller.enabled = false;
}


    void Detach()
    {
        isUsingZipline = false;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = true;

        player = null;
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

            // If we can't determine the hand, allow by default.
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
}
