using UnityEngine;

public class ShredderBlade : MonoBehaviour
{
    public float amplitude = 0.3f;   // How far up/down the blade moves
    public float speed = 20f;         // How fast it moves

    private Vector3 startLocalPos;

    void Start()
    {
        startLocalPos = transform.localPosition;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * amplitude;
        transform.localPosition = startLocalPos + Vector3.up * offset;
    }
}
