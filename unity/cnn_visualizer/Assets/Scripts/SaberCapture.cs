using UnityEngine;
using System.IO;
using System;

public class SaberCapture : MonoBehaviour
{
    public SaberPainter painter;
    public Transform drawPlane;
    public LayerMask strokesLayerMask;
    public bool whiteBackground = false;
    public int captureSize = 1024;

    Camera capCam;

    void Start()
    {
        var go = new GameObject("CaptureCam");
        capCam = go.AddComponent<Camera>();
        capCam.orthographic = true;
        capCam.enabled = false;
        capCam.cullingMask = strokesLayerMask;
        capCam.clearFlags = CameraClearFlags.SolidColor;
        capCam.backgroundColor = whiteBackground ? Color.white : new Color(0, 0, 0, 0);

        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        go.transform.position = drawPlane.position + Vector3.up * 5;
        capCam.orthographicSize = 5 * drawPlane.localScale.x;
    }

    public void CapturePNG()
    {
        var rt = new RenderTexture(captureSize, captureSize, 24, RenderTextureFormat.ARGB32);
        capCam.targetTexture = rt;
        RenderTexture.active = rt;
        capCam.Render();

        Texture2D tex = new Texture2D(captureSize, captureSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, captureSize, captureSize), 0, 0);
        tex.Apply();

        string dir = Application.persistentDataPath + "/SaberExports";
        Directory.CreateDirectory(dir);
        string path = $"{dir}/drawing_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log("Saved to: " + path);

        capCam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        Destroy(tex);
    }
}
