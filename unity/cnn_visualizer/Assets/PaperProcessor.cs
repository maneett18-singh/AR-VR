using UnityEngine;

public class PaperProcessor : MonoBehaviour
{
    public Material outputMaterial;   // Reusable material
    public Texture2D processedTexture; // Texture to apply to paper

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Paper"))
        {
            Renderer r = other.GetComponent<Renderer>();
            if (r != null)
            {
                r.material = outputMaterial;
                r.material.mainTexture = processedTexture;
            }
        }
    }
}
