using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ConveyorMover : MonoBehaviour
{
    [Header("Conveyor Settings")]
    public Vector3 localMoveDirection = Vector3.forward; // Local space movement
    public float speed = 1.5f;                           // Units/sec

    [HideInInspector] public bool isMoving = true;      // Controlled by Shredder

    private Collider conveyorCollider;

    private void Start()
    {
        // Cache the conveyor's collider for proper bounds
        conveyorCollider = GetComponent<Collider>();
        if (conveyorCollider == null)
            Debug.LogError("ConveyorMover requires a Collider on the same GameObject.");
    }

    private void FixedUpdate()
    {
        if (!isMoving) return;

        // Move all rigidbodies currently touching the conveyor using proper collider bounds
        Collider[] colliders = Physics.OverlapBox(
            conveyorCollider.bounds.center,    // Center of the collider
            conveyorCollider.bounds.extents,   // Half-size in each axis
            transform.rotation
        );

        foreach (Collider col in colliders)
        {
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                // Convert local direction to world
                Vector3 worldDir = transform.TransformDirection(localMoveDirection.normalized)*-1f;

                // Keep vertical motion (gravity) intact
                Vector3 newVelocity = worldDir * speed;
                newVelocity.y = rb.velocity.y;
                rb.velocity = newVelocity;
            }
        }
    }

    // Called by Shredder
    public void StartConveyor() => isMoving = true;

    public void StopConveyor() => isMoving = false;
}
