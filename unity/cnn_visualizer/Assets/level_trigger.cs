using UnityEngine;
using UnityEngine.InputSystem;

public class LeverTrigger : MonoBehaviour
{
    [Header("Machine Reference")]
    public Shredder_work shredder;
    public KeyCode interactKey = KeyCode.E;
    [Tooltip("Allow keyboard input (useful in editor).")]
    public bool allowKeyboardInput = false;
    [Tooltip("Optional XR input action to toggle the lever.")]
    public InputActionReference interactAction;

    private InputAction _interactRuntime;

    [Header("Lever Visual Settings")]
    public float toggleAngle = -90f;   // X rotation when ON
    public float smoothSpeed = 6f;

    [Header("Highlight Reference")]
    public RaycastVisibleOnKey raycastHighlight;

    private bool isMachineOn = true;

    private Transform leverTransform;
    private Quaternion offRotation;
    private Quaternion onRotation;
    private Quaternion targetRotation;

    void Start()
    {
        leverTransform = transform;

        float y = leverTransform.localEulerAngles.y;
        float z = leverTransform.localEulerAngles.z;

        offRotation = Quaternion.Euler(0f, y, z);
        onRotation  = Quaternion.Euler(toggleAngle, y, z);

        // Start ON
        targetRotation = onRotation;
        leverTransform.localRotation = onRotation;

        shredder?.StartMachine();
        Debug.Log("Lever initialized: ON (-90)");

        if (interactAction == null)
        {
            _interactRuntime = new InputAction("LeverInteract", InputActionType.Button);
            _interactRuntime.AddBinding("<XRController>{RightHand}/primaryButton");
            _interactRuntime.AddBinding("<PicoController>{RightHand}/primaryButton");
            _interactRuntime.AddBinding("<XRController>{LeftHand}/primaryButton");
            _interactRuntime.AddBinding("<PicoController>{LeftHand}/primaryButton");
            _interactRuntime.Enable();
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

    void Update()
    {
    bool interactPressed = allowKeyboardInput && Input.GetKeyDown(interactKey);
        if (interactAction != null && interactAction.action.WasPressedThisFrame())
            interactPressed = true;
        if (_interactRuntime != null && _interactRuntime.WasPressedThisFrame())
            interactPressed = true;

        if (raycastHighlight != null &&
            raycastHighlight.highlightedObject == gameObject &&
            gameObject.CompareTag("Lever") &&
            interactPressed)
        {
            ToggleLever();
        }

        // Smooth rotation
        leverTransform.localRotation =
            Quaternion.Lerp(leverTransform.localRotation, targetRotation, Time.deltaTime * smoothSpeed);
    }

    private void ToggleLever()
    {
        if (shredder == null)
        {
            Debug.LogError("LeverTrigger: Shredder reference missing");
            return;
        }

        if (isMachineOn)
        {
            shredder.StopMachine();
            targetRotation = offRotation;
            Debug.Log("Lever → OFF (0)");
        }
        else
        {
            shredder.StartMachine();
            targetRotation = onRotation;
            Debug.Log("Lever → ON (-90)");
        }

        isMachineOn = !isMachineOn;
        Debug.Log("Lever toggled");
    }
}
