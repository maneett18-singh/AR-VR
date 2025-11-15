using UnityEngine;
using System;

public static class ImageUtils
{
    public static Texture2D Base64ToTexture(string base64)
    {
        byte[] bytes = Convert.FromBase64String(base64);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        return tex;
    }
}
