using UnityEngine;
using System.IO;

public class WhiteboardExporter : MonoBehaviour
{
    public Camera captureCamera; // Assign your CaptureCamera here
    public int resWidth = 256;    // MNIST width
    public int resHeight = 256;   // MNIST height

    private static Texture2D Rotate90Clockwise(Texture2D src)
    {
        int w = src.width;
        int h = src.height;

        // New texture has swapped dimensions.
        Texture2D dst = new Texture2D(h, w, src.format, false);

        // (x, y) -> (h - 1 - y, x)
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                dst.SetPixel(h - 1 - y, x, src.GetPixel(x, y));
            }
        }

        dst.Apply();
        return dst;
    }

    void Update()
{
    // Input.GetKeyDown checks for the exact frame the key is pressed
    if (Input.GetKeyDown(KeyCode.P)) 
    {
        SaveDrawing(); // This calls your existing save logic
    }
}
    public void SaveDrawing()
    {
        // 1. Create a "bucket" for the pixels
        RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
        captureCamera.targetTexture = rt;

        // 2. Tell the camera to snap the photo
        Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
        captureCamera.Render();
        RenderTexture.active = rt;

        // 3. Read the pixels from the camera into the texture
        screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
        screenShot.Apply();

        // 4. Clean up the camera so it doesn't stay stuck on the texture
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);

    // 5. Rotate 90 degrees to the right (clockwise), then convert to PNG.
    Texture2D rotated = Rotate90Clockwise(screenShot);
    byte[] bytes = rotated.EncodeToPNG();
        string picsDir = Path.Combine(Application.dataPath, "Pics");
        Directory.CreateDirectory(picsDir);

        string filename = Path.Combine(
            picsDir,
            "MNIST_Drawing_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"
        );
        File.WriteAllBytes(filename, bytes);

        // Cleanup CPU-side textures
        Destroy(screenShot);
        Destroy(rotated);

        Debug.Log("Saved image to: " + filename);
    }
}