using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SaberStroke : MonoBehaviour
{
    LineRenderer lr;
    public float minPointDistance = 0.01f;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.positionCount = 0;
        lr.numCapVertices = 8;
        lr.numCornerVertices = 8;
        lr.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
    }

    public void Setup(Material mat, float width)
    {
        lr.material = mat;
        lr.startWidth = lr.endWidth = width;
    }

    public void AddPoint(Vector3 pos)
    {
        if (lr.positionCount == 0)
        {
            lr.positionCount = 1;
            lr.SetPosition(0, pos);
            return;
        }
        Vector3 last = lr.GetPosition(lr.positionCount - 1);
        if (Vector3.Distance(last, pos) < minPointDistance) return;
        lr.positionCount++;
        lr.SetPosition(lr.positionCount - 1, pos);
    }
}
