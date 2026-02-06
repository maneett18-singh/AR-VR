using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Feeds XR controller input into JetpackFlightController for jetpack movement.
/// Left stick = horizontal move. Triggers = vertical up/down.
/// </summary>
[RequireComponent(typeof(JetpackFlightController))]
public class PicoJetpackFlightInput : MonoBehaviour
{
    [Header("References")]
    public JetpackFlightController jetpack;

    [Header("Input Actions (Optional)")]
    public InputActionReference moveAction;       // Vector2
    public InputActionReference ascendAction;     // Float (trigger)
    public InputActionReference descendAction;    // Float (trigger)

    [Header("Tuning")]
    [Range(0f, 1f)] public float triggerDeadzone = 0.15f;

    private InputAction _moveRuntime;
    private InputAction _ascendRuntime;
    private InputAction _descendRuntime;

    private void Awake()
    {
        EnsureJetpackReference();

        if (moveAction == null)
        {
            _moveRuntime = new InputAction("JetpackMove", InputActionType.Value);
            _moveRuntime.AddCompositeBinding("2DVector")
                .With("Up", "<XRController>{LeftHand}/primary2DAxis/up")
                .With("Down", "<XRController>{LeftHand}/primary2DAxis/down")
                .With("Left", "<XRController>{LeftHand}/primary2DAxis/left")
                .With("Right", "<XRController>{LeftHand}/primary2DAxis/right");
            _moveRuntime.AddCompositeBinding("2DVector")
                .With("Up", "<PicoController>{LeftHand}/primary2DAxis/up")
                .With("Down", "<PicoController>{LeftHand}/primary2DAxis/down")
                .With("Left", "<PicoController>{LeftHand}/primary2DAxis/left")
                .With("Right", "<PicoController>{LeftHand}/primary2DAxis/right");
            _moveRuntime.Enable();
        }

        if (ascendAction == null)
        {
            _ascendRuntime = new InputAction("JetpackAscend", InputActionType.Value);
            _ascendRuntime.AddBinding("<XRController>{RightHand}/trigger");
            _ascendRuntime.AddBinding("<PicoController>{RightHand}/trigger");
            _ascendRuntime.Enable();
        }

        if (descendAction == null)
        {
            _descendRuntime = new InputAction("JetpackDescend", InputActionType.Value);
            _descendRuntime.AddBinding("<XRController>{LeftHand}/trigger");
            _descendRuntime.AddBinding("<PicoController>{LeftHand}/trigger");
            _descendRuntime.Enable();
        }
    }

    private void OnEnable()
    {
        EnsureJetpackReference();
        moveAction?.action.Enable();
        ascendAction?.action.Enable();
        descendAction?.action.Enable();

        _moveRuntime?.Enable();
        _ascendRuntime?.Enable();
        _descendRuntime?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        ascendAction?.action.Disable();
        descendAction?.action.Disable();

        _moveRuntime?.Disable();
        _ascendRuntime?.Disable();
        _descendRuntime?.Disable();

        if (jetpack != null)
            jetpack.SetExternalInput(Vector2.zero, 0f);
    }

    private void Update()
    {
        if (jetpack == null || !jetpack.hasJetpack || !jetpack.jetpackActive)
            return;

        Vector2 move = ReadMove();
        float up = ReadTrigger(ascendAction, _ascendRuntime);
        float down = ReadTrigger(descendAction, _descendRuntime);

        float vertical = Mathf.Clamp(up - down, -1f, 1f);
        jetpack.SetExternalInput(move, vertical);
    }

    private Vector2 ReadMove()
    {
        Vector2 value = Vector2.zero;
        if (moveAction != null)
            value = moveAction.action.ReadValue<Vector2>();
        else if (_moveRuntime != null)
            value = _moveRuntime.ReadValue<Vector2>();

        return value;
    }

    private float ReadTrigger(InputActionReference actionRef, InputAction runtime)
    {
        float value = 0f;
        if (actionRef != null)
            value = actionRef.action.ReadValue<float>();
        else if (runtime != null)
            value = runtime.ReadValue<float>();

        return value < triggerDeadzone ? 0f : value;
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
        if (jetpack == null)
            jetpack = GetComponentInParent<JetpackFlightController>();
    }
}