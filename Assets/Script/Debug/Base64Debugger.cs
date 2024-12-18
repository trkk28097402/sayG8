using System;
using System.IO;
using UnityEngine;

public class Base64ImageDebugger : MonoBehaviour
{
    public static void DebugBase64Image(string base64Image, string outputFileName = "DecodedImage.jpg")
    {
        try
        {
            if (string.IsNullOrEmpty(base64Image))
            {
                Debug.LogError("Base64 string is null or empty!");
                return;
            }

            byte[] imageBytes = Convert.FromBase64String(base64Image);

            Texture2D texture = new Texture2D(2, 2); 
            if (texture.LoadImage(imageBytes))
            {
                Debug.Log("Base64 image successfully decoded!");

                string filePath = Path.Combine(Application.persistentDataPath, outputFileName);
                File.WriteAllBytes(filePath, imageBytes);
                //Debug.Log($"Decoded image saved to: {filePath}");

                ShowTextureInScene(texture);
            }
            else
            {
                Debug.LogError("Failed to decode Base64 image to Texture2D!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error decoding Base64 image: {ex.Message}");
        }
    }

    private static void ShowTextureInScene(Texture2D texture)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = new Vector3(0, 0, 5); 
        quad.transform.localScale = new Vector3(5, 5, 1); 

        Material material = new Material(Shader.Find("Standard"));
        material.mainTexture = texture;

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.material = material;

        Debug.Log("Texture displayed in the scene.");
    }
}
