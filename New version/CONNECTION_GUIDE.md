# Unity3D ↔ Python Server Connection Guide

## 🔌 COMPLETE CONNECTION WORKFLOW

### **PHASE 1: STARTUP (First time you press Play in Unity)**

```
TERMINAL:
  python -m venv venv
  venv\Scripts\activate
  cd code
  uvicorn server:app --host 0.0.0.0 --port 8000
  
    ↓ (Server listens)
    
  ✅ Uvicorn running on http://0.0.0.0:8000
  ✅ WebSocket endpoint available at ws://localhost:8000/ws


UNITY:
  Click [Play]
  
    ↓ (UNetClient.Start() runs)
    
  ws = new WebSocket("ws://localhost:8000/ws")
  await ws.Connect()
  
    ↓
    
  🔗 CONNECTED!
  
  Console shows:
  ✅ WebSocket Connected to Server
```

---

### **PHASE 2: CONTINUOUS COMMUNICATION (Every frame)**

```
SERVER LOOP (Every 0.5 sec):
┌─────────────────────────────────────────────────────────┐
│ 1. Read damage_state (global variable)                 │
│    damage_state = 0.05 (starts at 5%)                  │
│                                                         │
│ 2. Generate PINN Inputs:                               │
│    For each of 2,500 grid points (50×50):              │
│      ├─ x = -525 to +525 (beam length)                │
│      ├─ y = -150 to +150 (beam height)                │
│      ├─ load_mag = 0 to 185,490 N                     │
│      ├─ global_deflection = 0 to 6.7 mm              │
│      ├─ fc = concrete strength                         │
│      └─ fy = steel strength                            │
│                                                         │
│ 3. Call PINN Model 2,500 times:                        │
│    stress_field = [pinn.predict(input) * (1 + 2.5*d)  │
│                     for each input]                    │
│                                                         │
│ 4. Calculate Max Stress:                               │
│    max_stress = max(stress_field)                      │
│                                                         │
│ 5. Update Damage:                                       │
│    growth = 0.002 + 0.00000004 * max_stress           │
│    damage_state += growth                              │
│                                                         │
│ 6. LSTM Input (if history full):                       │
│    history = last 10 × [max_stress, avg_stress]       │
│    damage_pred = lstm.predict(history)                 │
│    rul = (0.9 - damage_pred) / 0.5                    │
│                                                         │
│ 7. Send to Unity:                                      │
│    await ws.send_json({                                │
│      "time": 250.5,                                    │
│      "stress_field": [...2500 values...],             │
│      "damage_prediction": 0.52,                        │
│      "rul": 87.3                                       │
│    })                                                  │
└─────────────────────────────────────────────────────────┘
    ↓ (Network packet)
    ↓ (0.5 sec delay)
    ↓


UNITY RECEIVES:
┌─────────────────────────────────────────────────────────┐
│ ws.OnMessage += (bytes) => HandleMask(msg)             │
│                                                         │
│ 1. Deserialize JSON:                                    │
│    {                                                    │
│      time: 250.5,                                       │
│      stress_field: [array of 2500 floats],            │
│      damage_prediction: 0.52,                          │
│      rul: 87.3                                         │
│    }                                                    │
│                                                         │
│ 2. Update Visualizer:                                  │
│    For each vertex in beam mesh:                       │
│      ├─ Get corresponding stress from stress_field     │
│      ├─ Convert stress to RGB:                         │
│      │   • Green (0 stress)                            │
│      │   • Yellow (medium stress)                      │
│      │   • Red (high stress)                           │
│      └─ Set vertex color                               │
│                                                         │
│ 3. Apply Damage Location:                              │
│    ├─ Bottom-bias: Cracks prefer tension zone          │
│    ├─ Upward growth: Cracks grow as load increases    │
│    └─ Update color alpha based on damage_prediction   │
│                                                         │
│ 4. Display Text (optional):                            │
│    BeamText.text = $"Damage: {damage:.2%}             │
│                     RUL: {rul:.1f} steps              │
│                     Max Stress: {max:.1f} MPa"        │
│                                                         │
│ 5. Render Updated Mesh:                                │
│    mesh.colors = colors                                │
│    mesh.RecalculateNormals()                           │
└─────────────────────────────────────────────────────────┘
    ↓ (Frame rendered on screen)
    ↓
    
USER SEES:
┌──────────────────────────────────┐
│  3D Beam Visualization            │
│  ┌──────────────────────────────┐ │
│  │  ░░░░/\/\/\/\/\/\/░░░░░░░░░ │ │  ← Cracks (dark/red)
│  │  ░░░▓▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░ │ │  ← High stress (red)
│  │  ░░░░░░░░░░░░░░░░░░░░░░░░░░ │ │  ← Low stress (green)
│  │                              │ │
│  │  Damage: 52.3%  RUL: 87 steps│ │
│  └──────────────────────────────┘ │
└──────────────────────────────────┘
```

---

## 🎯 MAPPING: Stress Field → 3D Mesh Colors

The server sends 2,500 stress values (50×50 grid).
The Unity beam mesh has thousands of vertices.

**Mapping process**:
```
Beam Physical Space:                Server Grid:
┌──────────────────────────┐        [0,0] [1,0] [2,0] ... [50,50]
│                          │        ═════════════════════════════
│                          │        Stress values from PINN model
│  500×300 vertices        │        ↓
│  positioned at (x,y)     │        Normalize to beam coordinates
│                          │        ↓
└──────────────────────────┘        Map each vertex to grid point
                                    ↓
For each vertex at (x, y):         Sample stress value
├─ Find nearest grid point          ↓
├─ Get stress value                 Convert to RGB color:
├─ Convert stress to color:         • 0 stress = Green (0, 255, 0)
│  • Low stress → Green             • Med stress = Yellow (255, 255, 0)
│  • Med stress → Yellow            • High stress = Red (255, 0, 0)
│  • High stress → Red              ↓
└─ Set vertex color                 Apply to mesh vertex
                                    ↓
                           Render 3D beam with gradient colors
```

---

## 📨 MESSAGE FORMAT DETAILS

### **Server → Unity (every 0.5 sec)**

```json
{
    "time": 250.5,
    "stress_field": [
        1150.25,  // stress at grid[0,0]
        1200.50,  // stress at grid[1,0]
        1050.30,  // stress at grid[2,0]
        ...
        850.12    // stress at grid[50,50] (2500 total)
    ],
    "damage_prediction": 0.52,
    "rul": 87.3
}
```

**What each field means**:
- `time`: Simulation time elapsed (seconds)
- `stress_field`: Array of 2,500 stress values (Pa or MPa)
- `damage_prediction`: LSTM prediction (0 = new, 1 = failed)
- `rul`: Remaining useful life in time steps

---

## 🖼️ ALTERNATIVE: UNet Image Detection

If using `newServer.py` instead:

```
CAMERA CAPTURES IMAGE
    ↓
Texture2D in Unity memory
    ↓
UNetClient.SendImage(texture)
    ↓
Encode as Base64:
{
    "image": "iVBORw0KGgoAAAANSUhEUg..."  // 1MB+ string
}
    ↓
Send via WebSocket
    ↓
Server receives
    ├─ Decode Base64
    ├─ Resize to 512×512
    ├─ Normalize pixel values
    ├─ Pass through UNet
    ├─ Get crack mask (0=background, 1=crack)
    └─ Encode mask as Base64
        ↓
        Send back:
        {
            "mask": "iVBORw0KGgoAAAANSUhEUg..."
        }
    ↓
Unity receives mask
    ├─ Decode Base64 → Texture2D
    ├─ Invert colors (PyTorch format)
    ├─ Apply to beam material
    ├─ Extract crack height
    ├─ Convert to damage percentage
    └─ Send to DigitalTwinController
        ↓
        DigitalTwinController:
        ├─ Get crack height
        ├─ Estimate deflection
        ├─ Build PINN input array
        ├─ Run PINNRunner.Run(inputs)
        ├─ Get predicted load
        └─ Update visualizer with new damage level
            ↓
            Screen updates with new crack location
```

---

## ⚙️ TECHNICAL DETAILS: C# ↔ Python Communication

### **Unity Side (UNetClient.cs)**

```csharp
// 1. Initialize WebSocket
ws = new WebSocket("ws://localhost:8000/ws");

// 2. Set callbacks
ws.OnMessage += (bytes) =>
{
    string msg = Encoding.UTF8.GetString(bytes);  // Decode bytes
    HandleMask(msg);                               // Process JSON
};

// 3. Read message in Update()
ws.DispatchMessageQueue();  // Mandatory for non-WebGL platforms

// 4. Send data
await ws.SendText(json);
```

### **Python Side (server.py)**

```python
@app.websocket("/ws")
async def websocket_endpoint(ws: WebSocket):
    await ws.accept()  # Accept connection
    
    while True:
        # ✅ RECEIVE from Unity (if using UNet)
        # data = await ws.receive_text()
        
        # ✅ SEND to Unity (every 0.5 sec)
        await ws.send_json({
            "time": time,
            "stress_field": stress_field,
            "damage_prediction": damage_pred,
            "rul": rul
        })
        
        await asyncio.sleep(0.5)
```

---

## 🔄 DATA NORMALIZATION (Critical!)

**Problem**: ML models expect normalized data (mean=0, std=1)

**Solution**: ScalerManager

```csharp
// Before sending to PINN:
float normalized_load = scaler.Transform(185490f, scaler.parameters.load_mag);
// Result: (185490 - 105451.99) / 18936.68 = 4.23

// After PINN returns result:
float real_load = scaler.InverseTransform(pred_scaled, scaler.parameters.load_mag);
// Result: (pred_scaled * 18936.68) + 105451.99
```

**scaler_params.json mapping**:
```
Input Value → Normalized → ML Model → Output → Denormalized → Real Value
     ↑                                                               ↓
  185.5 kN              4.23            PINN            -0.5      155 kN
```

---

## ✅ CHECKLIST: GET IT RUNNING

### Server Setup ✓
- [ ] Python 3.10+ installed
- [ ] `pip install -r requirements.txt`
- [ ] `cd code`
- [ ] Server starts: `uvicorn server:app --host 0.0.0.0 --port 8000`
- [ ] Terminal shows: `✅ Uvicorn running on http://0.0.0.0:8000`

### Unity Setup ✓
- [ ] Assets/NativeWebSocket imported
- [ ] BeamDigitalTwin GameObject created
- [ ] MeshFilter + MeshRenderer added
- [ ] DigitalTwinVisualizer + DigitalTwinController scripts attached
- [ ] Material & Shader assigned
- [ ] PINNRunner script with model file assigned
- [ ] LSTMRunner script with model file assigned
- [ ] ScalerManager script with scaler_params_new.json assigned
- [ ] UNetClient pointing to DigitalTwinController

### Connection Test ✓
- [ ] Press Play
- [ ] Console shows: `✅ WebSocket Connected to Server`
- [ ] Console shows: `✅ Unity connected` (from server)
- [ ] Watch the 3D beam change colors as stress updates

### Visualization ✓
- [ ] Beam transitions from green (safe) → yellow (warning) → red (danger)
- [ ] Text displays damage % and RUL
- [ ] Cracks preferentially appear at bottom
- [ ] Cracks grow upward as load increases

---

**Connection Rate**: Every 0.5 seconds (2 Hz frequency)
**Latency**: ~10-50 ms (local network)
**Throughput**: ~200 KB per message (2,500 floats * 8 bytes = 20 KB data + JSON overhead)
