using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Proximity-based jetpack pickup for XR.
/// Walk near the jetpack and press A (primary button) to equip.
/// </summary>
[RequireComponent(typeof(Collider))]
public class XRJetpackProximityPickup : MonoBehaviour
{
    [Header("References")]
    public XRJetpackController jetpackController;
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

    [Header("Input")]
    public InputActionReference interactAction;
    public bool useRuntimeBindings = true;

    private InputAction _interactRuntime;
    private bool _picked;
    private GameObject _currentPlayer;
    private bool _playerInRange;

    private void Awake()
    {
        var trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;

        if (useRuntimeBindings && interactAction == null)
        {
            _interactRuntime = new InputAction("JetpackInteract", InputActionType.Button);
            _interactRuntime.AddBinding("<XRController>{RightHand}/primaryButton");
            _interactRuntime.AddBinding("<XRController>{LeftHand}/primaryButton");
            _interactRuntime.AddBinding("<PicoController>{RightHand}/primaryButton");
            _interactRuntime.AddBinding("<PicoController>{LeftHand}/primaryButton");
        }
    }

    private void OnEnable()
    {
        interactAction?.action.Enable();
        _interactRuntime?.Enable();
    }

    private void OnDisable()
    {
        interactAction?.action.Disable();
        _interactRuntime?.Disable();
    }

    private void Update()
    {
        if (_picked || !_playerInRange)
            return;

        bool pressed = false;
        if (interactAction != null)
            pressed = interactAction.action.WasPressedThisFrame();
        else if (_interactRuntime != null)
            pressed = _interactRuntime.WasPressedThisFrame();

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
            jetpackController = playerObj.GetComponent<XRJetpackController>();

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
        if (player == null)
            return;

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
