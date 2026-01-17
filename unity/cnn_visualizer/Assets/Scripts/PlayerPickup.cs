using UnityEngine;

public class PlayerPickup : MonoBehaviour
{
    [Header("References")]
    public Transform rayOrigin;                      // Ray origin (child of Player)
    public float pickupDistance = 2f;               // Distance along ray to hold object
    public Transform placeTarget;                   // Where the object will be dropped
    public RaycastVisibleOnKey highlightScript;     // Reference to your highlight system

    [Header("Settings")]
    public Vector3 holdOffset = new Vector3(0f, 0.5f, 0f); // Up/down offset while holding
    public float offsetStep = 0.1f;                     // How much each + or - key changes vertical offset

    private Rigidbody heldObject;
    private Collider heldCollider;

    void Update()
    {
        // Detect E key press
        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("E pressed");

            if (heldObject == null)
            {
                Debug.Log("Attempting to pick up object...");
                TryPickup();
            }
            else
            {
                Debug.Log("Attempting to drop object...");
                DropObject();
            }
        }

        // Adjust hold height at runtime
        if (heldObject != null)
        {
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus)) // + key
            {
                holdOffset.y += offsetStep;
                Debug.Log("Increased hold height: " + holdOffset.y);
            }
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.Underscore)) // - key
            {
                holdOffset.y -= offsetStep;
                Debug.Log("Decreased hold height: " + holdOffset.y);
            }
        }

        // Print when object is highlighted
        if (highlightScript != null && highlightScript.highlightedObject != null)
        {
            Debug.Log("Currently highlighted object: " + highlightScript.highlightedObject.name);
        }
    }

    void FixedUpdate()
    {
        // Smoothly move held object along the ray + vertical offset
        if (heldObject != null && rayOrigin != null)
        {
            Vector3 targetPos = rayOrigin.position + rayOrigin.forward * pickupDistance + holdOffset;
            heldObject.transform.position = Vector3.Lerp(heldObject.transform.position, targetPos, 15f * Time.fixedDeltaTime);
            heldObject.transform.rotation = Quaternion.Lerp(heldObject.transform.rotation, rayOrigin.rotation, 15f * Time.fixedDeltaTime);
        }
    }

   void TryPickup()
{
    if (highlightScript == null)
    {
        Debug.Log("Pickup failed: highlight script not assigned");
        return;
    }

    // Use highlighted or last highlighted object
    GameObject obj = highlightScript.highlightedObject != null
                     ? highlightScript.highlightedObject
                     : highlightScript.lastHighlightedObject;

    if (obj == null)
    {
        Debug.Log("Pickup failed: no object highlighted");
        return;
    }

    // Prevent picking up levers
    // Prevent picking up levers — DO NOTHING ELSE
    if (obj.CompareTag("Lever"))
    {
        Debug.Log("Pickup skipped: object is a lever");
        return;
    }


    Rigidbody rb = obj.GetComponent<Rigidbody>();
    Collider col = obj.GetComponent<Collider>();

    if (rb == null || col == null)
    {
        Debug.Log("Pickup failed: highlighted object requires Rigidbody and Collider");
        return;
    }

    // Assign held object
    heldObject = rb;
    heldCollider = col;

    // Disable physics while holding
    heldObject.isKinematic = true;
    heldObject.useGravity = false;
    heldCollider.enabled = false;

    Debug.Log("Pickup successful: " + obj.name + " now following ray from: " + rayOrigin.name + " with initial offset: " + holdOffset.y);
}

    void DropObject()
{
    if (heldObject == null) return;

    // Detach from player
    heldObject.transform.SetParent(null);

    // Enable physics
    heldObject.isKinematic = false;
    heldObject.useGravity = true;
    heldCollider.enabled = true;
    heldObject.collisionDetectionMode = CollisionDetectionMode.Continuous;

    // Move to placeTarget if assigned
    if (placeTarget != null)
    {
        Vector3 dropPos = placeTarget.position;

        // Lift by half the collider height to avoid intersecting conveyor/terrain
        if (heldCollider != null)
            dropPos.y += heldCollider.bounds.size.y / 2f;

        heldObject.transform.position = dropPos;
        heldObject.transform.rotation = placeTarget.rotation;
        Debug.Log("Dropped: " + heldObject.name + " at placeTarget: " + placeTarget.name);

        // Parent to conveyor if the tag matches
        if (placeTarget.CompareTag("Conveyor")) // <--- must assign this tag in Unity
        {
            heldObject.transform.SetParent(placeTarget);
            Debug.Log("Parented " + heldObject.name + " to conveyor: " + placeTarget.name);
        }
    }
    else
    {
        Vector3 dropPos = heldObject.transform.position;

        if (heldCollider != null)
            dropPos.y += heldCollider.bounds.size.y / 2f;

        heldObject.transform.position = dropPos;
        Debug.Log("Dropped: " + heldObject.name + " at current position");
    }

    heldObject = null;
    heldCollider = null;
}

}
