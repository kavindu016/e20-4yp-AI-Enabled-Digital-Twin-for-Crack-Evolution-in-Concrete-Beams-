using UnityEngine;
using System.Collections;

public class CrackSimulation : MonoBehaviour
{
    public int textureSize = 256;
    public Texture2D crackTexture;

    [Header("Simulation Timing")]
    public float timeBetweenSteps = 1.0f;
    public int pixelsPerStep = 10;
    public int totalSteps = 12;

    void Start()
    {
        crackTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Clear(); // This now generates concrete noise!
        crackTexture.Apply();

        Debug.Log("🛠️ CrackSimulation script started! Launching Coroutine...");
        StartCoroutine(GrowCrackGradually());
    }

    IEnumerator GrowCrackGradually()
    {
        UNetClient client = null;

        while (client == null || !client.IsConnected)
        {
            if (client == null) client = FindFirstObjectByType<UNetClient>();
            yield return new WaitForSeconds(1.0f);
        }

        Debug.Log("🚀 WebSocket Ready — Starting Gradual Crack Simulation!");

        int x = textureSize / 2;
        int y = 10;

        for (int step = 0; step < totalSteps; step++)
        {
            for (int i = 0; i < pixelsPerStep; i++)
            {
                // Draw a thick, dark gray/black crack
                for (int brushX = -2; brushX <= 2; brushX++)
                {
                    for (int brushY = -2; brushY <= 2; brushY++)
                    {
                        int drawX = Mathf.Clamp(x + brushX, 0, textureSize - 1);
                        int drawY = Mathf.Clamp(y + brushY, 0, textureSize - 1);
                        // Using a dark, jagged color instead of pure black
                        crackTexture.SetPixel(drawX, drawY, new Color(0.1f, 0.1f, 0.1f, 1f));
                    }
                }

                x += Random.Range(-2, 3);
                y += 1;

                if (x < 2) x = 2;
                if (x >= textureSize - 2) x = textureSize - 3;
                if (y >= textureSize - 2) break;
            }

            crackTexture.Apply();
            // client.SendImage(crackTexture); // Comment out the server for a moment
            DigitalTwinController dtc = Object.FindFirstObjectByType<DigitalTwinController>();
            if (dtc != null) dtc.ProcessMask(crackTexture);

            yield return new WaitForSeconds(timeBetweenSteps);
        }
    }

    // 🔄 THE FIX: Generate a noisy, gray background to fool the U-Net!
    void Clear()
    {
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            // Random gray values simulate the rough texture of real concrete
            float concreteNoise = Random.Range(0.45f, 0.65f);
            pixels[i] = new Color(concreteNoise, concreteNoise, concreteNoise, 1f);
        }
        crackTexture.SetPixels(pixels);
    }
}