using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Proximity pickup using XR InputDevices (A button / primary button).
/// Attach to the jetpack pickup object with a trigger collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class XRJetpackSimplePickup : MonoBehaviour
{
    [Header("References")]
    public XRJetpackSimpleController jetpackController;
    public Transform playerRoot;

    [Header("Attach")]
    public Transform jetpackVisualRoot;
    public string attachPointName = "JetpackAttach";
    public Vector3 localPositionOffset = Vector3.zero;
    public Vector3 localEulerOffset = Vector3.zero;

    [Header("Pickup")]
    public bool disableCollidersOnPickup = true;
    public bool disableRigidbodiesOnPickup = true;
    public bool destroyPickupAfterAttach = false;

    private bool _picked;
    private bool _playerInRange;
    private GameObject _currentPlayer;

    private void Awake()
    {
        var trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Update()
    {
        if (_picked || !_playerInRange)
            return;

        bool pressed = ReadPrimaryButton(XRNode.RightHand);
        if (!pressed)
            return;

        TryPickup();
    }

    private void TryPickup()
    {
        if (_picked)
            return;

        GameObject playerObj = playerRoot != null ? playerRoot.gameObject : _currentPlayer;
        if (playerObj == null)
            return;

        if (jetpackController == null)
            jetpackController = playerObj.GetComponent<XRJetpackSimpleController>();

        if (jetpackController == null)
            return;

        jetpackController.Equip(true);
        AttachToPlayer(playerObj.transform);
        ApplyPickupEffects();
        _picked = true;

        if (destroyPickupAfterAttach)
            Destroy(gameObject);
    }

    private void AttachToPlayer(Transform player)
    {
        Transform attachPoint = FindAttachPoint(player);
        Transform root = jetpackVisualRoot != null ? jetpackVisualRoot : transform;
        root.SetParent(attachPoint != null ? attachPoint : player, false);
        root.localPosition = localPositionOffset;
        root.localRotation = Quaternion.Euler(localEulerOffset);
    }

    private void ApplyPickupEffects()
    {
        if (disableCollidersOnPickup)
        {
            foreach (var c in GetComponentsInChildren<Collider>())
                c.enabled = false;
        }

        if (disableRigidbodiesOnPickup)
        {
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;
        }
    }

    private Transform FindAttachPoint(Transform player)
    {
        if (player == null)
            return null;

        if (!string.IsNullOrEmpty(attachPointName))
        {
            var direct = player.Find(attachPointName);
            if (direct != null)
                return direct;

            foreach (var child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == attachPointName)
                    return child;
            }
        }

        return player;
    }

    private bool ReadPrimaryButton(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed))
            return pressed;

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_playerInRange)
            return;

        _currentPlayer = playerRoot != null ? playerRoot.gameObject : other.transform.root.gameObject;
        _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentPlayer == null)
            return;

        if (other.transform.root.gameObject != _currentPlayer)
            return;

        _playerInRange = false;
        _currentPlayer = null;
    }
}
