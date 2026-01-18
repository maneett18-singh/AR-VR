using UnityEngine;
using UnityEngine.UI;

public class SobelFilterProcessor : MonoBehaviour
{
    public RawImage inputImage;   // Assign Input_Image here
    public RawImage outputImage;  // Assign Output_Image here
    public GridGenerator inputGrid;
    public GridGenerator outputGrid;

    private int[,] sobelX = new int[3, 3] {
        { -1, 0, 1 },
        { -2, 0, 2 },
        { -1, 0, 1 }
    };

    private int[,] sobelY = new int[3, 3] {
        { -1, -2, -1 },
        {  0,  0,  0 },
        {  1,  2,  1 }
    };

    void Start()
    {
        ApplySobel();
    }

    void ApplySobel()
    {
        if (inputImage.texture == null || outputImage == null) return;

        Texture2D tex = inputImage.texture as Texture2D;

        // Make a readable copy
        Texture2D readableTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
        readableTex.SetPixels(tex.GetPixels());
        readableTex.Apply();

        int width = readableTex.width;
        int height = readableTex.height;

        Texture2D resultTex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                float gx = 0;
                float gy = 0;

                // Apply Sobel kernels
                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        Color pixel = readableTex.GetPixel(x + kx, y + ky);
                        float intensity = pixel.grayscale;
                        gx += intensity * sobelX[ky + 1, kx + 1];
                        gy += intensity * sobelY[ky + 1, kx + 1];
                    }
                }

                // Gradient magnitude
                float g = Mathf.Sqrt(gx * gx + gy * gy);
                g = Mathf.Clamp01(g / 4f); // normalize

                // Highlight grid cells
                inputGrid.HighlightCell(y, x, new Color(1,1,1,0.3f));        // faint highlight
                outputGrid.HighlightCell(y, x, new Color(g, g, g, 0.7f));    // based on Sobel

                resultTex.SetPixel(x, y, new Color(g, g, g, 1));
            }
        }

        resultTex.Apply();
        outputImage.texture = resultTex;
    }
}
