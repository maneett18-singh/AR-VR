using UnityEngine;
using System.IO;
using UnityEngine.InputSystem;

public class WhiteboardExporter : MonoBehaviour
{
    public Camera captureCamera; // Assign your CaptureCamera here
    public int resWidth = 256;    // MNIST width
    public int resHeight = 256;   // MNIST height
    [Header("XR Input (Optional)")]
    [SerializeField] private InputActionReference captureAction; // e.g., X or A button

    void Update()
    {
        bool doCapture = captureAction != null ? captureAction.action.WasPressedThisFrame() : Input.GetKeyDown(KeyCode.P);
        if (doCapture)
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

        // 5. Convert to PNG bytes and save to your computer
        byte[] bytes = screenShot.EncodeToPNG();
        string filename = Application.dataPath + "/MNIST_Drawing_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        File.WriteAllBytes(filename, bytes);

        Debug.Log("Saved image to: " + filename);
    }
}