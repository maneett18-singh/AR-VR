using UnityEngine;

public class MousePanCamera : MonoBehaviour
{
    public float sensitivity = 2.0f;  // Mouse sensitivity
    public bool invertY = false;      // Optional Y inversion

    private float rotationX = 0f;     // Vertical rotation (pitch)
    private float rotationY = 0f;     // Horizontal rotation (yaw)

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        rotationX = angles.x;
        rotationY = angles.y;

        // Optional: lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Get mouse movement
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        // Update rotations
        rotationY += mouseX;
        rotationX += invertY ? mouseY : -mouseY;

        // Clamp vertical rotation to avoid flipping
        rotationX = Mathf.Clamp(rotationX, -90f, 90f);

        // Apply rotation
        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }
}
