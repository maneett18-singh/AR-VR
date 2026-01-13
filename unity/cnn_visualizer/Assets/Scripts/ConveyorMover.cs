using UnityEngine;

public class ConveyorMover : MonoBehaviour
{
    public Vector3 moveDirection = Vector3.forward;
    public float speed = 1.5f;

    private void OnCollisionStay(Collision collision)
    {
        Rigidbody rb = collision.rigidbody;
        if (rb != null)
        {
            rb.linearVelocity = moveDirection.normalized * speed;
        }
    }
}
