using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public float speed = 0.5f;           // Texture scroll speed
    private Renderer rend;
    private Vector2 offset;

    [HideInInspector] public bool isMoving = true;  // Controlled externally

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend == null)
            Debug.LogError("ConveyorBelt requires a Renderer on the same GameObject");
    }

    void Update()
    {
        if (!isMoving) return;  // Only scroll when conveyor is ON

        offset.y += speed * Time.deltaTime;
        rend.material.mainTextureOffset = offset;
    }

    // Control functions for Shredder
    public void StartConveyor() => isMoving = true;
    public void StopConveyor() => isMoving = false;
}
