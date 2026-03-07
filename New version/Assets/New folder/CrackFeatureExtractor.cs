using UnityEngine;

public static class CrackFeatureExtractor
{
    public static float CrackRatio(Texture2D mask)
    {
        if (mask == null) return 0f;

        Color[] pixels = mask.GetPixels();
        int crackCount = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            // black crack pixel
            if (pixels[i].r < 0.5f)
                crackCount++;
        }

        return (float)crackCount / pixels.Length;
    }
}