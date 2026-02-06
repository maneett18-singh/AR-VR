using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple XR jetpack controller (vertical movement only).
/// Attach to the player root that has a CharacterController.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class XRJetpackController : MonoBehaviour
{
    [Header("State")]
    public bool hasJetpack = false;
    public bool jetpackActive = false;

    [Header("Movement")]
    public float verticalSpeed = 4f;

    [Header("Input Actions (Optional)")]
    public InputActionReference ascendAction;   // Float (trigger)
    public InputActionReference descendAction;  // Float (trigger)

    [Header("Runtime Bindings")]
    public bool useRuntimeBindings = true;

    [Header("Locomotion Suppression")]
    [Tooltip("Optional: disable these behaviours while jetpack is active.")]
    public Behaviour[] disableOnActive;

    [Header("Keyboard (Editor)")]
    public bool allowKeyboardInput = false;
    public KeyCode ascendKey = KeyCode.Space;
    public KeyCode descendKey = KeyCode.LeftControl;

    private CharacterController _cc;
    private InputAction _ascendRuntime;
    private InputAction _descendRuntime;
    private bool[] _disableOnActiveWasEnabled;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        SetupRuntimeBindings();
    }

    private void OnEnable()
    {
        ascendAction?.action.Enable();
        descendAction?.action.Enable();
        _ascendRuntime?.Enable();
        _descendRuntime?.Enable();
    }

    private void OnDisable()
    {
        ascendAction?.action.Disable();
        descendAction?.action.Disable();
        _ascendRuntime?.Disable();
        _descendRuntime?.Disable();
    }

    private void Update()
    {
        if (!hasJetpack || !jetpackActive)
            return;

        float up = ReadTrigger(ascendAction, _ascendRuntime);
        float down = ReadTrigger(descendAction, _descendRuntime);

        if (allowKeyboardInput)
        {
            if (Input.GetKey(ascendKey)) up = 1f;
            if (Input.GetKey(descendKey)) down = 1f;
        }

        float vertical = Mathf.Clamp(up - down, -1f, 1f);
        if (Mathf.Abs(vertical) < 0.01f)
            return;

        Vector3 move = Vector3.up * (vertical * verticalSpeed);
        _cc.Move(move * Time.deltaTime);
    }

    public void Equip(bool activate = true)
    {
        hasJetpack = true;
        jetpackActive = activate;
        CacheDisableStates();
        SetDisableOnActive(jetpackActive);
    }

    public void SetActive(bool active)
    {
        if (!hasJetpack)
            return;

        jetpackActive = active;
        SetDisableOnActive(active);
    }

    public void Unequip()
    {
        hasJetpack = false;
        jetpackActive = false;
        SetDisableOnActive(false);
    }

    private void SetupRuntimeBindings()
    {
        if (!useRuntimeBindings)
            return;

        if (ascendAction == null)
        {
            _ascendRuntime = new InputAction("JetpackAscend", InputActionType.Value);
            _ascendRuntime.AddBinding("<XRController>{RightHand}/trigger");
            _ascendRuntime.AddBinding("<PicoController>{RightHand}/trigger");
        }

        if (descendAction == null)
        {
            _descendRuntime = new InputAction("JetpackDescend", InputActionType.Value);
            _descendRuntime.AddBinding("<XRController>{LeftHand}/trigger");
            _descendRuntime.AddBinding("<PicoController>{LeftHand}/trigger");
        }
    }

    private float ReadTrigger(InputActionReference actionRef, InputAction runtime)
    {
        if (actionRef != null)
            return actionRef.action.ReadValue<float>();

        if (runtime != null)
            return runtime.ReadValue<float>();

        return 0f;
    }

    private void CacheDisableStates()
    {
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
}
