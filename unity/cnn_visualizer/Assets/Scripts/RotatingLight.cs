using UnityEngine;

public class RotatingTower : MonoBehaviour
{
    [Header("Beacon Settings")]
    public Transform rotatingPart;       // Assign the light or mesh to rotate
    public float rotationSpeed = 60f;    // Degrees per second
    public bool rotateClockwise = true;  // Direction of rotation
    public bool isRotating = true;       // Enable/disable rotation

    [Header("Optional Swing (for scanning effect)")]
    public bool swing = false;           // Swing back and forth
    public float swingAngle = 45f;       // Max swing angle in degrees
    public float swingSpeed = 1f;        // Speed of swinging

    private Quaternion initialRotation;
    private float swingTimer = 0f;

    void Start()
    {
        if (rotatingPart == null)
        {
            Debug.LogError("RotatingPart not assigned!");
            enabled = false;
            return;
        }

        initialRotation = rotatingPart.localRotation;
    }

    void Update()
    {
        if (!isRotating) return;

        if (swing)
        {
            // Swing back and forth like a scanning beacon
            swingTimer += Time.deltaTime * swingSpeed;
            float angle = Mathf.Sin(swingTimer * Mathf.PI * 2f) * swingAngle * 0.5f; // -swingAngle/2 -> +swingAngle/2
            rotatingPart.localRotation = initialRotation * Quaternion.Euler(0f, angle, 0f);
        }
        else
        {
            // Continuous rotation
            float dir = rotateClockwise ? 1f : -1f;
            rotatingPart.Rotate(Vector3.up * rotationSpeed * dir * Time.deltaTime, Space.Self);
        }
    }

    // Optional controls
    public void StartRotation() => isRotating = true;
    public void StopRotation() => isRotating = false;
}
