using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class XRCarProximityInteract : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public Transform playerRoot;

    [Header("Input")]
    [Tooltip("Optional XR input action to enter/exit the car (Button).")]
    public InputActionReference interactAction;

    [Header("Range")]
    [Tooltip("Max distance allowed to interact if trigger exit is missed.")]
    public float maxInteractDistance = 3f;

    [Header("Fallback Bindings")]
    public bool useRuntimeBindings = true;

    private InputAction _interactRuntime;
    private GameObject currentPlayer;
    private bool playerInRange;

    private void Awake()
    {
        if (car == null)
            car = GetComponentInParent<CarController>();

        var trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;

        if (useRuntimeBindings && interactAction == null)
        {
            _interactRuntime = new InputAction("CarInteract", InputActionType.Button);
            _interactRuntime.AddBinding("<XRController>{LeftHand}/primaryButton");
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
        if (playerRoot != null && playerInRange)
        {
            float distance = Vector3.Distance(playerRoot.position, transform.position);
            if (distance > maxInteractDistance)
            {
                playerInRange = false;
                currentPlayer = null;
            }
        }

        if (!playerInRange || car == null)
            return;

        bool pressed = false;
        if (interactAction != null)
            pressed = interactAction.action.WasPressedThisFrame();
        else if (_interactRuntime != null)
            pressed = _interactRuntime.WasPressedThisFrame();

        if (!pressed)
            return;

        if (car.IsDriving)
            car.RequestExitCar();
        else if (currentPlayer != null)
            car.EnterCar(currentPlayer);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerInRange)
            return;

        if (playerRoot != null)
        {
            if (other.transform.root.gameObject != playerRoot.gameObject)
                return;

            currentPlayer = playerRoot.gameObject;
            playerInRange = true;
            return;
        }

        currentPlayer = other.transform.root.gameObject;
        playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (currentPlayer == null)
            return;

        if (other.transform.root.gameObject != currentPlayer)
            return;

        playerInRange = false;
        currentPlayer = null;
    }
}