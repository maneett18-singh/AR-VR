using UnityEngine;
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

    private Quaternion carCameraBaseLocalRotation = Quaternion.identity;

    private Rigidbody rb;
    private float moveInput;
    private float turnInput;

    private GameObject playerObj;
    // 🟢 FIXED: Using the full name to avoid Namespace vs Class error
    private AstronautPlayer.AstronautPlayer playerScript;
    private Camera playerCamera;
    private bool isDriving = false;

    private Rigidbody playerRb;
    private bool playerRbWasKinematic;
    private bool playerRbHadGravity;

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

    void Update()
    {
        if (isDriving)
        {
            moveInput = Input.GetAxis("Vertical");
            turnInput = Input.GetAxis("Horizontal");

            DriveLookLogic();
            if (Input.GetKeyDown(exitKey) || Input.GetKeyDown(exitAltKey))
                ExitCar();
        }
    }

    private void FixedUpdate()
    {
        if (!isDriving)
            return;

        DriveMovementPhysics();
    }

    public void RequestExitCar()
    {
        if (isDriving)
            ExitCar();
    }

    public void EnterCar(GameObject player)
    {
        playerObj = player;
        // 🟢 FIXED: Full name used here as well
        playerScript = player.GetComponent<AstronautPlayer.AstronautPlayer>();
        
        if (playerScript == null)
        {
            Debug.LogError("AstronautPlayer script not found on the player object!");
            return;
        }

        playerCamera = playerScript.playerCamera;

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
        player.transform.localRotation = Quaternion.identity;

        // 3. Swap Cameras
        if (playerCamera != null) playerCamera.gameObject.SetActive(false);
        if (carCamera != null) carCamera.gameObject.SetActive(true);

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

        if (playerObj == null || playerScript == null)
            return;

        // 1. Unparent and Re-enable Player
        playerObj.transform.SetParent(null);
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