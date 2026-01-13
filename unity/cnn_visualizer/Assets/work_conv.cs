using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public float speed = 0.5f;
    private Renderer rend;
    private Vector2 offset;

    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    void Update()
    {
        offset.y += speed * Time.deltaTime;
        rend.material.mainTextureOffset = offset;
    }
}
