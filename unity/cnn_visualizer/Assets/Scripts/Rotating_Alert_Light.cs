using UnityEngine;

public class BeaconLight : MonoBehaviour
{
    [Header("Light Settings")]
    public Light beacon;               // Assign the Light component (Spot recommended)
    public bool isOn = true;           // Initial state
    public bool blink = true;          // Optional blinking
    public float blinkSpeed = 2f;      // Pulses per second

    [Header("Rotation Settings")]
    public bool rotate = true;         // Optional rotation
    public Vector3 rotationAxis = Vector3.up; // Axis to rotate the beacon mesh
    public float rotationSpeed = 60f;  // Degrees per second

    private float timer = 0f;

    void Start()
    {
        // Auto-assign Light if not manually assigned
        if (beacon == null)
        {
            beacon = GetComponent<Light>();
            if (beacon == null)
            {
                Debug.LogError("BeaconLight requires a Light component on this GameObject");
                enabled = false;
                return;
            }
        }

        beacon.enabled = isOn;
    }

    void Update()
    {
        // Handle rotation of the beacon
        if (rotate)
        {
            transform.Rotate(rotationAxis.normalized * rotationSpeed * Time.deltaTime, Space.Self);
        }

        // Handle light on/off
        if (!isOn)
        {
            beacon.enabled = false;
            return;
        }

        if (blink)
        {
            timer += Time.deltaTime;
            // Use sine wave for smooth blinking
            beacon.enabled = Mathf.Sin(timer * Mathf.PI * blinkSpeed) > 0f;
        }
        else
        {
            beacon.enabled = true;
        }
    }

    // Optional external controls
    public void TurnOn() => isOn = true;
    public void TurnOff() => isOn = false;
    public void Toggle() => isOn = !isOn;
}
