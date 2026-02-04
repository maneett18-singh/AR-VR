using System.Collections.Generic;

[System.Serializable]
public class FeatureMapData
{
    public int[] shape;
    public string base64;
}

[System.Serializable]
public class WebSocketData
{
    public string input_image_base64;
    public int[] input_image_shape;
    public Dictionary<string, Dictionary<string, FeatureMapData>> feature_maps;
}
