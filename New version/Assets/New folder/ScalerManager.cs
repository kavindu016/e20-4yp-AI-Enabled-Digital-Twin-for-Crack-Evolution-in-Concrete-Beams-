using UnityEngine;
using System;

// These classes exactly match your JSON structure
[Serializable]
public class ScalerFeature
{
    public float mean;
    public float scale;
    public string type;
}

[Serializable]
public class ScalerParams
{
    public ScalerFeature x;
    public ScalerFeature y;
    public ScalerFeature load_mag;
    public ScalerFeature global_deflection;
    public ScalerFeature fc;
    public ScalerFeature fy;
}

public class ScalerManager : MonoBehaviour
{
    public TextAsset jsonFile; // Drag scaler_params_new.json here in the Inspector
    public ScalerParams parameters;

    void Awake()
    {
        if (jsonFile != null)
        {
            parameters = JsonUtility.FromJson<ScalerParams>(jsonFile.text);
            Debug.Log("✅ Scaler params loaded successfully.");
        }
        else
        {
            Debug.LogError("❌ Scaler JSON not assigned!");
        }
    }

    // Use this BEFORE sending data to the PINN
    public float Transform(float value, ScalerFeature feature)
    {
        return (value - feature.mean) / feature.scale;
    }

    // Use this AFTER getting data back from the PINN
    public float InverseTransform(float scaledValue, ScalerFeature feature)
    {
        return (scaledValue * feature.scale) + feature.mean;
    }
}