using UnityEngine;

/// <summary>
/// Simple jetpack flight using CharacterController.
/// Assumes a separate script handles mouse look (e.g., AstronautPlayer or PlayerMovement).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class JetpackFlightController : MonoBehaviour
{
    [Header("State")]
    public bool hasJetpack = false;
    public bool jetpackActive = false;

    [Header("Controls")]
        [Tooltip("Allow keyboard input for jetpack (useful in editor).")]
        public bool allowKeyboardInput = false;

        [Tooltip("Use externally supplied input (XR controller).")]
        public bool useExternalInput = true;

    public KeyCode ascendKey = KeyCode.Space;
    [Tooltip("Optional descend key (not required, but useful).")]
    public KeyCode descendKey = KeyCode.LeftControl;

    [Tooltip("Toggle jetpack flight on/off (returns to walking when off).")]
    public KeyCode toggleJetpackKey = KeyCode.F;

    [Tooltip("Drop/leave the jetpack at the current place.")]
    public KeyCode dropJetpackKey = KeyCode.I;

    [Header("Movement")]
    public float flySpeed = 8f;
    public float strafeSpeed = 8f;
    public float verticalSpeed = 6f;

    [Header("References")]
    [Tooltip("If null, uses Camera.main.")]
    public Camera referenceCamera;

    private CharacterController _cc;
    private AstronautPlayer.AstronautPlayer _astronautPlayer;

    private JetpackPickup _equippedPickup;
    private Vector2 _externalMoveInput;
    private float _externalVerticalInput;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _astronautPlayer = GetComponent<AstronautPlayer.AstronautPlayer>();
        if (referenceCamera == null)
            referenceCamera = Camera.main;
    }

    public bool EquipAndActivate()
    {
        hasJetpack = true;
        jetpackActive = true;

        if (_astronautPlayer != null)
            _astronautPlayer.SetMovementSuppressed(true);
        return true;
    }

    public void RegisterEquippedPickup(JetpackPickup pickup)
    {
        _equippedPickup = pickup;
        hasJetpack = true;
    }

    public void SetActive(bool active)
    {
        if (!hasJetpack)
            return;
        jetpackActive = active;

        if (_astronautPlayer != null)
            _astronautPlayer.SetMovementSuppressed(active);
    }

    private void Update()
    {
        if (hasJetpack)
        {
            if (allowKeyboardInput && Input.GetKeyDown(toggleJetpackKey))
                SetActive(!jetpackActive);

            if (allowKeyboardInput && Input.GetKeyDown(dropJetpackKey))
                DropJetpack();
        }

        if (!hasJetpack || !jetpackActive)
            return;

        // Horizontal movement relative to where the player is facing (yaw).
        float v = 0f;
        float h = 0f;

        if (useExternalInput)
        {
            v = _externalMoveInput.y;
            h = _externalMoveInput.x;
        }
        else if (allowKeyboardInput)
        {
            v = Input.GetAxisRaw("Vertical");   // W/S
            h = Input.GetAxisRaw("Horizontal"); // A/D
        }

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 horizontal = (forward * v * flySpeed) + (right * h * strafeSpeed);
        if (horizontal.sqrMagnitude > 0.0001f)
            horizontal = Vector3.ClampMagnitude(horizontal, Mathf.Max(flySpeed, strafeSpeed));

        float y = 0f;
        if (useExternalInput)
        {
            y = Mathf.Clamp(_externalVerticalInput, -1f, 1f);
        }
        else if (allowKeyboardInput)
        {
            if (Input.GetKey(ascendKey)) y += 1f;
            if (Input.GetKey(descendKey)) y -= 1f;
        }

        Vector3 vertical = Vector3.up * (y * verticalSpeed);

        Vector3 move = horizontal + vertical;
        _cc.Move(move * Time.deltaTime);
    }

    public bool DropJetpack()
    {
        if (!hasJetpack)
            return false;

        // Turn off flight and resume walking immediately.
        SetActive(false);
        hasJetpack = false;

        if (_equippedPickup == null)
            return true;

        Transform root = _equippedPickup.jetpackVisualRoot != null ? _equippedPickup.jetpackVisualRoot : _equippedPickup.transform;
        root.SetParent(null, true);

        // Place slightly in front of the player so it doesn't overlap.
        root.position = transform.position + (transform.forward * 1.0f) + (Vector3.up * 0.5f);
        root.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        _equippedPickup.MakePickableAgain();
        _equippedPickup = null;
        return true;
    }

    public void SetExternalInput(Vector2 moveInput, float verticalInput)
    {
        _externalMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        _externalVerticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
    }
}
