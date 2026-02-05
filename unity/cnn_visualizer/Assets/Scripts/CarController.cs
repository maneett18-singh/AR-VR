using UnityEngine;
using UnityEngine.InputSystem;
// We don't need 'using AstronautPlayer;' if we use the full name below

public class CarController : MonoBehaviour
{
    [Header("References")]
    [Header("Interior Look")]
public float lookSensitivity = 2f;
private float rotX;
private float rotY;
    public Transform seatPoint; 
    public Camera carCamera; 
    public float driveSpeed = 20f;
    public float turnSpeed = 100f;

    public enum DriveAxis
    {
        Forward,
        Right
    }

    [Header("Driving")]
    public DriveAxis driveAxis = DriveAxis.Forward;
    public bool invertDriveAxis = false;

    [Header("XR Input (Optional)")]
    [Tooltip("Vector2 input for movement. Typically Left Stick (primary2DAxis).")]
    public InputActionReference moveAction;

    [Tooltip("Vector2 input for turning. Typically Right Stick (primary2DAxis).")]
    public InputActionReference turnAction;

    [Tooltip("Button input to exit the car.")]
    public InputActionReference exitAction;

    [Tooltip("If enabled, keyboard input still works when no XR action is assigned.")]
    public bool allowKeyboardInput = true;

    [Header("Desktop Look (Optional)")]
    [Tooltip("Disable this for VR so mouse look doesn't override headset rotation.")]
    public bool enableMouseLookInsideCar = true;

    [Tooltip("If true, switches to the car camera even when a VR stereo camera is active.")]
    public bool forceCarCameraInVr = false;

    [Header("Player Control Suppression")]
    [Tooltip("Optional: XR locomotion components to disable while driving (e.g., LocomotionSystem, MoveProvider, TeleportationProvider).")]
    public Behaviour[] disableOnEnter;

    [Tooltip("Keep the player locked to the seat while driving.")]
    public bool lockPlayerToSeat = true;

    [Tooltip("Use Rigidbody movement for proper collision with terrain/rocks.")]
    public bool useRigidbodyMovement = true;

    [Tooltip("Helps keep the car upright (prevents tipping).")]
    public bool freezePitchAndRoll = true;

    [Header("Exit")]
    public KeyCode exitKey = KeyCode.F;
    public KeyCode exitAltKey = KeyCode.E;

    [Tooltip("Optional. If set, player exits at this transform.")]
    public Transform exitPoint;

    [Tooltip("Used when exitPoint is not set.")]
    public Vector3 exitOffsetLocal = new Vector3(2.5f, 0f, 0f);

    [Tooltip("Optional. Set this to an empty child transform that faces the FRONT of the car. Used for movement direction + camera alignment.")]
    public Transform forwardReference;

    [Tooltip("If enabled, entering the car will align the car camera to face the forwardReference.")]
    public bool alignCameraToForwardReferenceOnEnter = true;

    [Tooltip("If enabled, entering the car will rotate the player to face the car's forwardReference.")]
    public bool alignPlayerToForwardReferenceOnEnter = true;

    private Quaternion carCameraBaseLocalRotation = Quaternion.identity;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;

    private InputAction _moveRuntime;
    private InputAction _turnRuntime;
    private InputAction _exitRuntime;

    private GameObject playerObj;
    // Optional: only used when the player actually has AstronautPlayer
    private AstronautPlayer.AstronautPlayer playerScript;
    private Camera playerCamera;
    private bool isDriving = false;

    private Rigidbody playerRb;
    private bool playerRbWasKinematic;
    private bool playerRbHadGravity;

    private bool[] disableOnEnterWasEnabled;

    private void Awake()
    {
        SetupRuntimeInputActions();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.useGravity = true;
            if (freezePitchAndRoll)
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        if (carCamera != null) carCamera.gameObject.SetActive(false);
        if (carCamera != null)
            carCameraBaseLocalRotation = carCamera.transform.localRotation;
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
        turnAction?.action.Enable();
        exitAction?.action.Enable();

        _moveRuntime?.Enable();
        _turnRuntime?.Enable();
        _exitRuntime?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        turnAction?.action.Disable();
        exitAction?.action.Disable();

        _moveRuntime?.Disable();
        _turnRuntime?.Disable();
        _exitRuntime?.Disable();
    }

    void Update()
    {
        if (isDriving)
        {
            ReadDrivingInput();

            if (enableMouseLookInsideCar && allowKeyboardInput)
                DriveLookLogic();

            if (ReadExitPressed())
                ExitCar();
        }
    }

    private void FixedUpdate()
    {
        if (!isDriving)
            return;

        DriveMovementPhysics();
    }

    private void LateUpdate()
    {
        if (!isDriving || !lockPlayerToSeat || playerObj == null || seatPoint == null)
            return;

        if (playerObj.transform.parent != seatPoint)
            playerObj.transform.SetParent(seatPoint);

        playerObj.transform.localPosition = Vector3.zero;
        playerObj.transform.localRotation = Quaternion.identity;
    }

    public void RequestExitCar()
    {
        if (isDriving)
            ExitCar();
    }

    private void ReadDrivingInput()
    {
        bool hasMoveAction = moveAction != null || _moveRuntime != null;
        bool hasTurnAction = turnAction != null || _turnRuntime != null;

        if (hasMoveAction)
        {
            Vector2 move = ReadMove();
            float driveValue = (driveAxis == DriveAxis.Right) ? move.x : move.y;
            moveInput = invertDriveAxis ? -driveValue : driveValue;
        }
        else if (allowKeyboardInput)
        {
            moveInput = Input.GetAxis("Vertical");
        }
        else
        {
            moveInput = 0f;
        }

        if (hasTurnAction)
        {
            Vector2 turn = ReadTurn();
            turnInput = turn.x;
        }
        else if (allowKeyboardInput)
        {
            turnInput = Input.GetAxis("Horizontal");
        }
        else
        {
            turnInput = 0f;
        }
    }

    private Vector2 ReadMove()
    {
        if (moveAction != null)
            return moveAction.action.ReadValue<Vector2>();

        return _moveRuntime != null ? _moveRuntime.ReadValue<Vector2>() : Vector2.zero;
    }

    private Vector2 ReadTurn()
    {
        if (turnAction != null)
            return turnAction.action.ReadValue<Vector2>();

        return _turnRuntime != null ? _turnRuntime.ReadValue<Vector2>() : Vector2.zero;
    }

    private bool ReadExitPressed()
    {
        if (exitAction != null)
            return exitAction.action.WasPressedThisFrame();

        if (_exitRuntime != null && _exitRuntime.WasPressedThisFrame())
            return true;

        return allowKeyboardInput && (Input.GetKeyDown(exitKey) || Input.GetKeyDown(exitAltKey));
    }

    private void SetupRuntimeInputActions()
    {
        if (moveAction == null)
        {
            _moveRuntime = new InputAction("CarMove", InputActionType.Value);
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
        }

        if (turnAction == null)
        {
            _turnRuntime = new InputAction("CarTurn", InputActionType.Value);
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
        }

        if (exitAction == null)
        {
            _exitRuntime = new InputAction("CarExit", InputActionType.Button);
            _exitRuntime.AddBinding("<XRController>{RightHand}/primaryButton");
            _exitRuntime.AddBinding("<XRController>{LeftHand}/primaryButton");
            _exitRuntime.AddBinding("<PicoController>{RightHand}/primaryButton");
            _exitRuntime.AddBinding("<PicoController>{LeftHand}/primaryButton");
        }
    }

    public bool IsDriving => isDriving;

    public void EnterCar(GameObject player)
    {
        if (isDriving)
            return;

        if (player == null)
            return;

        playerObj = player;
        // Optional: only used if present
        playerScript = player.GetComponent<AstronautPlayer.AstronautPlayer>();

    CacheDisableStates();
    SetDisableOnEnter(false);

        if (playerScript != null && playerScript.playerCamera != null)
        {
            playerCamera = playerScript.playerCamera;
        }
        else
        {
            playerCamera = player.GetComponentInChildren<Camera>();
            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        // Ensure reference transforms are part of the moving car hierarchy.
        // If they live elsewhere in the scene, the player/camera won't move with the car.
        if (seatPoint == null)
        {
            Debug.LogError("SeatPoint is not assigned on CarController!");
            return;
        }
        if (!seatPoint.IsChildOf(transform))
        {
            Debug.LogWarning("SeatPoint is not a child of the car. Re-parenting it to the car so the player moves with the car.");
            seatPoint.SetParent(transform, true);
        }
        if (carCamera != null && !carCamera.transform.IsChildOf(transform))
        {
            Debug.LogWarning("CarCamera is not a child of the car. Re-parenting it to the car so the view moves with the car.");
            carCamera.transform.SetParent(transform, true);
        }
        if (exitPoint != null && !exitPoint.IsChildOf(transform))
            exitPoint.SetParent(transform, true);
        if (forwardReference != null && !forwardReference.IsChildOf(transform))
            forwardReference.SetParent(transform, true);

        // 1. Disable Player Movement & Physics
        if (playerScript != null)
            playerScript.enabled = false;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRbWasKinematic = playerRb.isKinematic;
            playerRbHadGravity = playerRb.useGravity;
            playerRb.isKinematic = true;
            playerRb.useGravity = false;
        }

        // 2. Parent Player to Seat
        player.transform.SetParent(seatPoint);
        player.transform.localPosition = Vector3.zero;
        if (alignPlayerToForwardReferenceOnEnter && forwardReference != null)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, forwardReference.rotation.eulerAngles.y, 0f);
            player.transform.rotation = targetRotation;
            player.transform.localPosition = Vector3.zero;
        }
        else
        {
            player.transform.localRotation = Quaternion.identity;
        }

        // 3. Swap Cameras
        bool isVrCamera = playerCamera != null && playerCamera.stereoEnabled;
        if (!isVrCamera || forceCarCameraInVr)
        {
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);
            if (carCamera != null) carCamera.gameObject.SetActive(true);
        }

        // Reset look state so we don't start with a random offset
        rotX = 0f;
        rotY = 0f;

        // If the car/camera is oriented sideways, align the in-car camera to the car's forward reference.
        if (carCamera != null)
        {
            if (alignCameraToForwardReferenceOnEnter && forwardReference != null)
                carCamera.transform.rotation = forwardReference.rotation;

            carCameraBaseLocalRotation = carCamera.transform.localRotation;
        }

        isDriving = true;
    }

    void ExitCar()
    {
        isDriving = false;

        if (playerObj == null)
            return;

        // 1. Unparent and Re-enable Player
        playerObj.transform.SetParent(null);
        if (playerScript != null)
            playerScript.enabled = true;

        var cc = playerObj.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        if (playerRb != null)
        {
            playerRb.isKinematic = playerRbWasKinematic;
            playerRb.useGravity = playerRbHadGravity;
            playerRb = null;
        }

        // 2. Restore Player Camera
    if (carCamera != null) carCamera.gameObject.SetActive(false);
    if (playerCamera != null) playerCamera.gameObject.SetActive(true);

        // 3. Place player outside the car so they don't get stuck
        if (exitPoint != null)
        {
            playerObj.transform.position = exitPoint.position;
            playerObj.transform.rotation = exitPoint.rotation;
        }
        else
        {
            playerObj.transform.position = transform.TransformPoint(exitOffsetLocal);
        }

        if (cc != null) cc.enabled = true;

        SetDisableOnEnter(true);
    }

    private void CacheDisableStates()
    {
        if (disableOnEnter == null || disableOnEnter.Length == 0)
            return;

        disableOnEnterWasEnabled = new bool[disableOnEnter.Length];
        for (int i = 0; i < disableOnEnter.Length; i++)
        {
            var behaviour = disableOnEnter[i];
            disableOnEnterWasEnabled[i] = behaviour != null && behaviour.enabled;
        }
    }

    private void SetDisableOnEnter(bool restore)
    {
        if (disableOnEnter == null || disableOnEnter.Length == 0)
            return;

        for (int i = 0; i < disableOnEnter.Length; i++)
        {
            var behaviour = disableOnEnter[i];
            if (behaviour == null)
                continue;

            if (restore)
            {
                if (disableOnEnterWasEnabled != null && i < disableOnEnterWasEnabled.Length)
                    behaviour.enabled = disableOnEnterWasEnabled[i];
            }
            else
            {
                behaviour.enabled = false;
            }
        }
    }

    private void DriveMovementPhysics()
    {
        // Use the car's own axes by default, or the forwardReference axes if provided.
        Transform reference = (forwardReference != null) ? forwardReference : transform;
        Vector3 moveAxisWorld = (driveAxis == DriveAxis.Right) ? reference.right : reference.forward;
        if (invertDriveAxis) moveAxisWorld = -moveAxisWorld;
        moveAxisWorld = moveAxisWorld.normalized;

        float move = moveInput * driveSpeed;
        float yaw = turnInput * turnSpeed;

        // If no Rigidbody (or disabled), fall back to transform movement.
        if (rb == null || !useRigidbodyMovement)
        {
            transform.position += moveAxisWorld * (move * Time.deltaTime);
            transform.Rotate(0f, yaw * Time.deltaTime, 0f, Space.Self);
            return;
        }

        Vector3 delta = moveAxisWorld * (move * Time.fixedDeltaTime);
        rb.MovePosition(rb.position + delta);

        Quaternion deltaRot = Quaternion.Euler(0f, yaw * Time.fixedDeltaTime, 0f);
        rb.MoveRotation(rb.rotation * deltaRot);
    }

    private void DriveLookLogic()
    {
        // POV Look Logic (Look around inside the car)
        rotX += Input.GetAxis("Mouse X") * lookSensitivity;
        rotY -= Input.GetAxis("Mouse Y") * lookSensitivity;
        rotY = Mathf.Clamp(rotY, -40f, 40f); // Limit how far you can look up/down
        rotX = Mathf.Clamp(rotX, -70f, 70f); // Limit how far you can look left/right

        if (carCamera != null)
            carCamera.transform.localRotation = carCameraBaseLocalRotation * Quaternion.Euler(rotY, rotX, 0);
    }
}