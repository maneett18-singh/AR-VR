using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Simple jetpack that uses XR InputDevices triggers directly (no Input Actions).
/// Attach to the XR Origin root that has a CharacterController.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class XRJetpackSimpleController : MonoBehaviour
{
    [Header("State")]
    public bool hasJetpack = false;
    public bool jetpackActive = false;

    [Header("Movement")]
    public float verticalSpeed = 5f;

    [Tooltip("How quickly to correct vertical drift when holding altitude.")]
    public float hoverSnapSpeed = 15f;

    [Header("Hover")]
    [Tooltip("If true, the player will not fall while jetpack is active unless descending.")]
    public bool holdAltitudeWhileActive = true;

    [Header("Input Mode")]
    [Tooltip("Use X/Y (primary/secondary buttons) for ascend/descend instead of triggers.")]
    public bool useButtonsForVertical = true;

    [Header("Locomotion Suppression")]
    public Behaviour[] disableOnActive;
    [Tooltip("Automatically finds and disables locomotion/move/teleport components when jetpack is active.")]
    public bool autoDisableLocomotion = true;

    [Header("Physics Suppression")]
    [Tooltip("Disable gravity on all child rigidbodies while jetpack is active.")]
    public bool disableChildGravityWhileActive = true;

    [Tooltip("Set child rigidbodies to kinematic while jetpack is active.")]
    public bool setChildKinematicWhileActive = true;

    private CharacterController _cc;
    private bool[] _disableOnActiveWasEnabled;
    private float _targetY;
    private bool _hasTargetY;
    private Rigidbody[] _childRigidbodies;
    private bool[] _childRbWasKinematic;
    private bool[] _childRbHadGravity;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!hasJetpack || !jetpackActive)
            return;

    float up = useButtonsForVertical ? ReadPrimaryButton(XRNode.LeftHand) : ReadTrigger(XRNode.RightHand);
    float down = useButtonsForVertical ? ReadSecondaryButton(XRNode.LeftHand) : ReadTrigger(XRNode.LeftHand);

        float verticalInput = Mathf.Clamp(up - down, -1f, 1f);

        if (holdAltitudeWhileActive)
        {
            if (!_hasTargetY)
            {
                _targetY = transform.position.y;
                _hasTargetY = true;
            }

            if (Mathf.Abs(verticalInput) < 0.01f)
            {
                _targetY = transform.position.y;
            }
            else
            {
                _targetY = Mathf.Max(_targetY, transform.position.y);
                _targetY += (verticalInput * verticalSpeed * Time.deltaTime);
            }
            float deltaY = _targetY - transform.position.y;
            float snap = Mathf.Clamp(deltaY, -hoverSnapSpeed * Time.deltaTime, hoverSnapSpeed * Time.deltaTime);

            if (Mathf.Abs(snap) < 0.0001f)
                return;

            _cc.Move(Vector3.up * snap);
            return;
        }

        float finalVertical = verticalInput * verticalSpeed;
        if (Mathf.Abs(finalVertical) < 0.01f)
            return;

        _cc.Move(Vector3.up * finalVertical * Time.deltaTime);
    }

    public void Equip(bool activate = true)
    {
        hasJetpack = true;
        jetpackActive = activate;
        if (autoDisableLocomotion)
            AutoFindLocomotionComponents();
        _targetY = transform.position.y;
        _hasTargetY = true;
        CacheDisableStates();
        SetDisableOnActive(jetpackActive);
        CacheChildRigidbodies();
        SetChildRigidbodiesActive(jetpackActive);
    }

    public void Unequip()
    {
        hasJetpack = false;
        jetpackActive = false;
        _hasTargetY = false;
        SetDisableOnActive(false);
        SetChildRigidbodiesActive(false);
    }

    public void SetActive(bool active)
    {
        if (!hasJetpack)
            return;

        jetpackActive = active;
        if (jetpackActive && autoDisableLocomotion)
            AutoFindLocomotionComponents();
        if (jetpackActive)
        {
            _targetY = transform.position.y;
            _hasTargetY = true;
        }
        else
        {
            _hasTargetY = false;
        }
        SetDisableOnActive(active);
        SetChildRigidbodiesActive(active);
    }

    private float ReadTrigger(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return 0f;

        if (device.TryGetFeatureValue(CommonUsages.trigger, out float value))
            return value;

        return 0f;
    }

    private float ReadPrimaryButton(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return 0f;

        if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed))
            return pressed ? 1f : 0f;

        return 0f;
    }

    private float ReadSecondaryButton(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return 0f;

        if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool pressed))
            return pressed ? 1f : 0f;

        return 0f;
    }

    private void CacheDisableStates()
    {
        if ((disableOnActive == null || disableOnActive.Length == 0) && autoDisableLocomotion)
            AutoFindLocomotionComponents();

        if (disableOnActive == null || disableOnActive.Length == 0)
            return;

        _disableOnActiveWasEnabled = new bool[disableOnActive.Length];
        for (int i = 0; i < disableOnActive.Length; i++)
        {
            var behaviour = disableOnActive[i];
            _disableOnActiveWasEnabled[i] = behaviour != null && behaviour.enabled;
        }
    }

    private void SetDisableOnActive(bool active)
    {
        if (disableOnActive == null || disableOnActive.Length == 0)
            return;

        for (int i = 0; i < disableOnActive.Length; i++)
        {
            var behaviour = disableOnActive[i];
            if (behaviour == null)
                continue;

            if (active)
            {
                behaviour.enabled = false;
            }
            else if (_disableOnActiveWasEnabled != null && i < _disableOnActiveWasEnabled.Length)
            {
                behaviour.enabled = _disableOnActiveWasEnabled[i];
            }
        }
    }

    private void AutoFindLocomotionComponents()
    {
        var behaviours = GetComponents<Behaviour>();
        var matches = new System.Collections.Generic.List<Behaviour>();

        foreach (var behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this)
                continue;

            var typeName = behaviour.GetType().Name;
            if (typeName.Contains("Locomotion") ||
                typeName.Contains("MoveProvider") ||
                typeName.Contains("Teleport") ||
                typeName.Contains("SnapTurn") ||
                typeName.Contains("ContinuousMove"))
            {
                matches.Add(behaviour);
            }
        }

        if (matches.Count > 0)
            disableOnActive = matches.ToArray();
    }

    private void CacheChildRigidbodies()
    {
        if (!disableChildGravityWhileActive && !setChildKinematicWhileActive)
            return;

        _childRigidbodies = GetComponentsInChildren<Rigidbody>(true);
        if (_childRigidbodies == null || _childRigidbodies.Length == 0)
            return;

        _childRbWasKinematic = new bool[_childRigidbodies.Length];
        _childRbHadGravity = new bool[_childRigidbodies.Length];

        for (int i = 0; i < _childRigidbodies.Length; i++)
        {
            var rb = _childRigidbodies[i];
            if (rb == null)
                continue;

            _childRbWasKinematic[i] = rb.isKinematic;
            _childRbHadGravity[i] = rb.useGravity;
        }
    }

    private void SetChildRigidbodiesActive(bool active)
    {
        if (_childRigidbodies == null || _childRigidbodies.Length == 0)
            return;

        for (int i = 0; i < _childRigidbodies.Length; i++)
        {
            var rb = _childRigidbodies[i];
            if (rb == null)
                continue;

            if (active)
            {
                if (disableChildGravityWhileActive)
                    rb.useGravity = false;
                if (setChildKinematicWhileActive)
                    rb.isKinematic = true;
            }
            else
            {
                if (_childRbHadGravity != null && i < _childRbHadGravity.Length)
                    rb.useGravity = _childRbHadGravity[i];
                if (_childRbWasKinematic != null && i < _childRbWasKinematic.Length)
                    rb.isKinematic = _childRbWasKinematic[i];
            }
        }
    }
}
