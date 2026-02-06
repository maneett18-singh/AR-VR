using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles jetpack toggle and unequip input for XR.
/// Attach to the same GameObject as XRJetpackController (player root).
/// </summary>
[RequireComponent(typeof(XRJetpackController))]
public class XRJetpackInputToggle : MonoBehaviour
{
    [Header("References")]
    public XRJetpackController jetpack;

    [Header("Input Actions (Optional)")]
    [Tooltip("Toggle jetpack active on/off (e.g., thumbstick click).")]
    public InputActionReference toggleAction;

    [Tooltip("Unequip jetpack (e.g., secondary button).")]
    public InputActionReference unequipAction;

    [Header("Runtime Bindings")]
    public bool useRuntimeBindings = true;

    private InputAction _toggleRuntime;
    private InputAction _unequipRuntime;

    private void Awake()
    {
        if (jetpack == null)
            jetpack = GetComponent<XRJetpackController>();

        if (useRuntimeBindings && toggleAction == null)
        {
            _toggleRuntime = new InputAction("JetpackToggle", InputActionType.Button);
            _toggleRuntime.AddBinding("<XRController>{LeftHand}/primary2DAxisClick");
            _toggleRuntime.AddBinding("<XRController>{RightHand}/primary2DAxisClick");
            _toggleRuntime.AddBinding("<PicoController>{LeftHand}/primary2DAxisClick");
            _toggleRuntime.AddBinding("<PicoController>{RightHand}/primary2DAxisClick");
        }

        if (useRuntimeBindings && unequipAction == null)
        {
            _unequipRuntime = new InputAction("JetpackUnequip", InputActionType.Button);
            _unequipRuntime.AddBinding("<XRController>{LeftHand}/secondaryButton");
            _unequipRuntime.AddBinding("<XRController>{RightHand}/secondaryButton");
            _unequipRuntime.AddBinding("<PicoController>{LeftHand}/secondaryButton");
            _unequipRuntime.AddBinding("<PicoController>{RightHand}/secondaryButton");
        }
    }

    private void OnEnable()
    {
        toggleAction?.action.Enable();
        unequipAction?.action.Enable();
        _toggleRuntime?.Enable();
        _unequipRuntime?.Enable();
    }

    private void OnDisable()
    {
        toggleAction?.action.Disable();
        unequipAction?.action.Disable();
        _toggleRuntime?.Disable();
        _unequipRuntime?.Disable();
    }

    private void Update()
    {
        if (jetpack == null || !jetpack.hasJetpack)
            return;

        bool togglePressed = toggleAction != null && toggleAction.action.WasPressedThisFrame();
        bool unequipPressed = unequipAction != null && unequipAction.action.WasPressedThisFrame();

        if (_toggleRuntime != null && _toggleRuntime.WasPressedThisFrame())
            togglePressed = true;
        if (_unequipRuntime != null && _unequipRuntime.WasPressedThisFrame())
            unequipPressed = true;

        if (togglePressed)
            jetpack.SetActive(!jetpack.jetpackActive);

        if (unequipPressed)
            jetpack.Unequip();
    }
}
