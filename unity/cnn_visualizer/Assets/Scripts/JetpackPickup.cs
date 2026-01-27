using UnityEngine;

/// <summary>
/// Put this on the Jetpack prefab/object. When the player looks at it and presses E,
/// it equips the jetpack and optionally attaches this object to the player.
/// </summary>
public class JetpackPickup : MonoBehaviour
{
    [Header("Attach")]
    [Tooltip("If set, this transform will be parented to the player's attachPoint on pickup.")]
    public Transform jetpackVisualRoot;

    [Tooltip("If set, attach to this child name on the player (e.g., 'JetpackAttach', 'Spine', etc.).")]
    public string attachPointName = "JetpackAttach";

    [Tooltip("Local position offset after attaching.")]
    public Vector3 localPositionOffset = Vector3.zero;

    [Tooltip("Local rotation offset after attaching.")]
    public Vector3 localEulerOffset = Vector3.zero;

    [Header("Pickup")]
    public bool disableCollidersOnPickup = true;
    public bool disableRigidbodiesOnPickup = true;
    public bool destroyPickupAfterAttach = false;

    private bool _picked;

    public bool TryPickup(GameObject player)
    {
        if (_picked || player == null)
            return false;

        var flight = player.GetComponent<JetpackFlightController>();
        if (flight == null)
            return false;

        flight.EquipAndActivate();
        flight.RegisterEquippedPickup(this);

        Transform attachPoint = FindAttachPoint(player.transform);
        if (attachPoint != null)
        {
            Transform root = jetpackVisualRoot != null ? jetpackVisualRoot : transform;
            root.SetParent(attachPoint, false);
            root.localPosition = localPositionOffset;
            root.localRotation = Quaternion.Euler(localEulerOffset);
        }

        if (disableCollidersOnPickup)
        {
            foreach (var c in GetComponentsInChildren<Collider>())
                c.enabled = false;
        }

        if (disableRigidbodiesOnPickup)
        {
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
                rb.isKinematic = true;
        }

        _picked = true;

        if (destroyPickupAfterAttach)
        {
            Destroy(gameObject);
        }

        return true;
    }

    public void MakePickableAgain()
    {
        _picked = false;

        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = true;

        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = false;
    }

    private Transform FindAttachPoint(Transform player)
    {
        if (player == null) return null;
        if (string.IsNullOrEmpty(attachPointName)) return player;

        // Try direct child name
        var t = player.Find(attachPointName);
        if (t != null) return t;

        // Search in children
        foreach (var child in player.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == attachPointName)
                return child;
        }

        return player;
    }
}
