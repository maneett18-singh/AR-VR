using UnityEngine;

public class Zipline : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    public Transform player; // assign your player object in inspector

    public float speed = 5f;

    private bool isUsingZipline;
    private float t;

    void Update()
    {
        if (!isUsingZipline || player == null) return;

        t += Time.deltaTime * speed / Vector3.Distance(startPoint.position, endPoint.position);
        player.position = Vector3.Lerp(startPoint.position, endPoint.position, t);

        if (t >= 1f)
        {
            Detach();
        }
    }

    public void Attach(Transform playerTransform)
{
    Debug.Log("Player attached: " + playerTransform.name);
    player = playerTransform;
    t = 0f;
    isUsingZipline = true;

    var controller = player.GetComponent<CharacterController>();
    if (controller != null) controller.enabled = false;
}


    void Detach()
    {
        isUsingZipline = false;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = true;

        player = null;
    }
}
