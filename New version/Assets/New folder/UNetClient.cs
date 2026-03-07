using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;

public class UNetClient : MonoBehaviour
{
    WebSocket ws;

    public bool IsConnected
    {
        get { return ws != null && ws.State == WebSocketState.Open; }
    }

    public Material beamMaterial;
    public DigitalTwinController controller;

    async void Start()
    {
        ws = new WebSocket("ws://localhost:8000/ws");

        ws.OnOpen += () => Debug.Log("✅ WebSocket Connected to Server");
        ws.OnError += (e) => Debug.LogError("❌ WebSocket Error: " + e);
        ws.OnClose += (e) => Debug.LogWarning("⚠ WebSocket Closed");

        ws.OnMessage += (bytes) =>
        {
            string msg = Encoding.UTF8.GetString(bytes);
            HandleMask(msg);
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

    public async void SendImage(Texture2D tex)
    {
        if (!IsConnected) return;

        string base64 = Convert.ToBase64String(tex.EncodeToPNG());
        string json = "{\"image\":\"" + base64 + "\"}";

        await ws.SendText(json);
    }

    void HandleMask(string json)
    {
        MaskData data = JsonUtility.FromJson<MaskData>(json);
        byte[] maskBytes = Convert.FromBase64String(data.mask);

        Texture2D tex = new Texture2D(512, 512);
        tex.LoadImage(maskBytes);

        // 🔄 THE FIX: Invert the PyTorch mask so Unity understands it!
        // Turns Black Background -> White | Turns White Cracks -> Black
        Color[] pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float invertedColor = 1.0f - pixels[i].r;
            pixels[i] = new Color(invertedColor, invertedColor, invertedColor, 1.0f);
        }
        tex.SetPixels(pixels);
        tex.Apply();

        // Send the inverted mask to the controller
        if (controller != null)
            controller.ProcessMask(tex);
    }

    [Serializable]
    public class MaskData { public string mask; }
}