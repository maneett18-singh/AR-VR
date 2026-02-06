using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

/// <summary>
/// Simple controller-based locomotion for an XR Origin (XR Rig).
/// Uses the left stick for movement and the right stick for turning by default.
/// </summary>
public class XRControllerLocomotion : MonoBehaviour
{
    [Header("References")]
    [Tooltip("XR Origin root. If empty, will search on this GameObject.")]
    public XROrigin xrOrigin;

    [Tooltip("Optional override for the forward direction (defaults to XR Origin Camera).")]
    public Transform forwardSource;

    [Header("Input Actions (Optional)")]
    [Tooltip("2D axis for movement (left stick). If empty, runtime bindings will be created.")]
    public InputActionReference moveAction;

    [Tooltip("2D axis for turning (right stick). If empty, runtime bindings will be created.")]
    public InputActionReference turnAction;

    [Header("Movement Settings")]
    public float moveSpeed = 2.5f;
    public float turnSpeed = 90f;
    public float gravity = 9.81f;
    public float deadzone = 0.15f;

    [Tooltip("Keep movement on the ground plane (ignores camera pitch).")]
    public bool keepGrounded = true;

    private CharacterController _characterController;
    private InputAction _moveRuntime;
    private InputAction _turnRuntime;
    private float _verticalVelocity;

    private void Awake()
    {
        EnsureReferences();

        if (moveAction == null)
        {
            _moveRuntime = new InputAction("XR Move", InputActionType.Value);
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

        if (turnAction == null)
        {
            _turnRuntime = new InputAction("XR Turn", InputActionType.Value);
            _turnRuntime.AddCompositeBinding("2DVector")
                .With("Up", "<XRController>{RightHand}/primary2DAxis/up")
                .With("Down", "<XRController>{RightHand}/primary2DAxis/down")
                .With("Left", "<XRController>{RightHand}/primary2DAxis/left")
                .With("Right", "<XRController>{RightHand}/primary2DAxis/right");
            _turnRuntime.AddCompositeBinding("2DVector")
                .With("Up", "<PicoController>{RightHand}/primary2DAxis/up")
                .With("Down", "<PicoController>{RightHand}/primary2DAxis/down")
                .With("Left", "<PicoController>{RightHand}/primary2DAxis/left")
                .With("Right", "<PicoController>{RightHand}/primary2DAxis/right");
            _turnRuntime.Enable();
        }
    }

    private void OnEnable()
    {
        EnsureReferences();
        if (moveAction != null)
            moveAction.action.Enable();
        if (turnAction != null)
            turnAction.action.Enable();

        _moveRuntime?.Enable();
        _turnRuntime?.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null)
            moveAction.action.Disable();
        if (turnAction != null)
            turnAction.action.Disable();

        _moveRuntime?.Disable();
        _turnRuntime?.Disable();
    }

    private void Update()
    {
        if (xrOrigin == null)
            return;

        var moveInput = ReadMoveInput();
        var turnInput = ReadTurnInput();

        if (turnInput.sqrMagnitude > 0f)
        {
            float yaw = turnInput.x * turnSpeed * Time.deltaTime;
            xrOrigin.transform.Rotate(0f, yaw, 0f, Space.World);
        }

        Vector3 moveDirection = Vector3.zero;
        if (moveInput.sqrMagnitude > 0f)
        {
            var source = forwardSource != null ? forwardSource : xrOrigin.Camera?.transform;
            Vector3 forward = source != null ? source.forward : xrOrigin.transform.forward;
            Vector3 right = source != null ? source.right : xrOrigin.transform.right;

            if (keepGrounded)
            {
                forward.y = 0f;
                right.y = 0f;
            }

            forward.Normalize();
            right.Normalize();
            moveDirection = (forward * moveInput.y + right * moveInput.x) * moveSpeed;
        }

        ApplyMovement(moveDirection);
    }

    private void ApplyMovement(Vector3 horizontalVelocity)
    {
        if (_characterController == null)
        {
            xrOrigin.transform.position += horizontalVelocity * Time.deltaTime;
            return;
        }

        if (_characterController.isGrounded)
            _verticalVelocity = -0.5f;
        else
            _verticalVelocity -= gravity * Time.deltaTime;

        Vector3 velocity = new Vector3(horizontalVelocity.x, _verticalVelocity, horizontalVelocity.z);
        _characterController.Move(velocity * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 value = Vector2.zero;
        if (moveAction != null)
            value = moveAction.action.ReadValue<Vector2>();
        else if (_moveRuntime != null)
            value = _moveRuntime.ReadValue<Vector2>();

        return ApplyDeadzone(value);
    }

    private Vector2 ReadTurnInput()
    {
        Vector2 value = Vector2.zero;
        if (turnAction != null)
            value = turnAction.action.ReadValue<Vector2>();
        else if (_turnRuntime != null)
            value = _turnRuntime.ReadValue<Vector2>();

        return ApplyDeadzone(value);
    }

    private Vector2 ApplyDeadzone(Vector2 input)
    {
        if (input.magnitude < deadzone)
            return Vector2.zero;

        return input;
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (xrOrigin == null)
            xrOrigin = GetComponent<XROrigin>();

        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();
    }
}