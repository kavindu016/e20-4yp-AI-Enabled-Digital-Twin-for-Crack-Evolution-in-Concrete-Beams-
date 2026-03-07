using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Collections.Generic;

public class PhysicsServerClient : MonoBehaviour
{
    WebSocket ws;
    public DigitalTwinVisualizer visualizer;
    
    [Serializable]
    public class ServerResponse
    {
        public float time;
        public float[] stress_field;
        public float damage_prediction;
        public float rul;
    }

    async void Start()
    {
        ws = new WebSocket("ws://localhost:8000/ws");

        ws.OnOpen += () => Debug.Log("✅ Connected to Physics Server (server.py)");
        ws.OnError += (e) => Debug.LogError("❌ WebSocket Error: " + e);
        ws.OnClose += (e) => Debug.LogWarning("⚠️ WebSocket Closed");

        ws.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            HandleServerData(msg);
        };

        await ws.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (ws != null)
            ws.DispatchMessageQueue();
#endif
    }

    void HandleServerData(string json)
    {
        try
        {
            ServerResponse data = JsonUtility.FromJson<ServerResponse>(json);
            
            if (visualizer != null)
            {
                // Map damage prediction to load value for visualization
                // Assume: 0 damage = 0 load, 1 damage = max load
                float maxLoadN = 185490f;
                float estimatedLoad = data.damage_prediction * maxLoadN;
                
                visualizer.loadVal = estimatedLoad;

                // Optional: Log the data
                Debug.Log($"📊 Server Data | Time: {data.time:F1}s | Damage: {data.damage_prediction:F2} | RUL: {data.rul:F1}s | Stress Points: {data.stress_field.Length}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error parsing server data: {e.Message}");
        }
    }

    async void OnDestroy()
    {
        if (ws != null)
            await ws.Close();
    }
}
