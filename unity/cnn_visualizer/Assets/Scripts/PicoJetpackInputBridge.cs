using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Bridges Pico/XR controller inputs to JetpackFlightController toggle/drop actions.
/// Attach this to the same GameObject as JetpackFlightController (usually your XR Origin root).
/// </summary>
[RequireComponent(typeof(JetpackFlightController))]
public class PicoJetpackInputBridge : MonoBehaviour
{
    [Header("References")]
    public JetpackFlightController jetpack;

    [Header("XR Input (Optional)")]
    [Tooltip("Action for toggling jetpack on/off (e.g., thumbstick click).")]
    public InputActionReference toggleAction;

    [Tooltip("Action for dropping the jetpack (e.g., secondary thumbstick click).")]
    public InputActionReference dropAction;

    [Header("Keyboard Fallback (Editor)")]
    public KeyCode toggleKey = KeyCode.F;
    public KeyCode dropKey = KeyCode.I;

    private InputAction _toggleRuntime;
    private InputAction _dropRuntime;

    private void Awake()
    {
        EnsureJetpackReference();

        if (toggleAction == null)
        {
            _toggleRuntime = new InputAction("JetpackToggle", InputActionType.Button);
            _toggleRuntime.AddBinding("<XRController>{LeftHand}/primary2DAxisClick");
            _toggleRuntime.AddBinding("<XRController>{RightHand}/primary2DAxisClick");
            _toggleRuntime.AddBinding("<PicoController>{LeftHand}/primary2DAxisClick");
            _toggleRuntime.AddBinding("<PicoController>{RightHand}/primary2DAxisClick");
            _toggleRuntime.Enable();
        }

        if (dropAction == null)
        {
            _dropRuntime = new InputAction("JetpackDrop", InputActionType.Button);
            _dropRuntime.AddBinding("<XRController>{LeftHand}/secondary2DAxisClick");
            _dropRuntime.AddBinding("<XRController>{RightHand}/secondary2DAxisClick");
            _dropRuntime.AddBinding("<PicoController>{LeftHand}/secondary2DAxisClick");
            _dropRuntime.AddBinding("<PicoController>{RightHand}/secondary2DAxisClick");
            _dropRuntime.Enable();
        }
    }

    private void OnEnable()
    {
        EnsureJetpackReference();
        if (toggleAction != null)
            toggleAction.action.Enable();
        if (dropAction != null)
            dropAction.action.Enable();

        if (_toggleRuntime != null)
            _toggleRuntime.Enable();
        if (_dropRuntime != null)
            _dropRuntime.Enable();
    }

    private void OnDisable()
    {
        if (toggleAction != null)
            toggleAction.action.Disable();
        if (dropAction != null)
            dropAction.action.Disable();

        if (_toggleRuntime != null)
            _toggleRuntime.Disable();
        if (_dropRuntime != null)
            _dropRuntime.Disable();
    }

    private void Update()
    {
        if (jetpack == null || !jetpack.hasJetpack)
            return;

        bool togglePressed = Input.GetKeyDown(toggleKey);
        bool dropPressed = Input.GetKeyDown(dropKey);

        if (toggleAction != null && toggleAction.action.WasPressedThisFrame())
            togglePressed = true;
        if (dropAction != null && dropAction.action.WasPressedThisFrame())
            dropPressed = true;

        if (_toggleRuntime != null && _toggleRuntime.WasPressedThisFrame())
            togglePressed = true;
        if (_dropRuntime != null && _dropRuntime.WasPressedThisFrame())
            dropPressed = true;

        if (togglePressed)
            jetpack.SetActive(!jetpack.jetpackActive);

        if (dropPressed)
            jetpack.DropJetpack();
    }

    private void OnValidate()
    {
        EnsureJetpackReference();
    }

    private void EnsureJetpackReference()
    {
        if (jetpack != null)
            return;

        jetpack = GetComponent<JetpackFlightController>();
        if (jetpack != null)
            return;

        jetpack = GetComponentInParent<JetpackFlightController>();
        if (jetpack != null)
            return;

        jetpack = GetComponentInChildren<JetpackFlightController>();
    }
}
