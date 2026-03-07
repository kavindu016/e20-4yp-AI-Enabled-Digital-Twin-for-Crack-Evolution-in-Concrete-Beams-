# AI-Enabled Digital Twin for Crack Evolution in Concrete Beams
## Complete Architecture & Workflow Guide

---

## 🎯 PROJECT OVERVIEW

This is a **Physics-Informed Digital Twin** system that:
- **Monitors** crack evolution in concrete beams in real-time
- **Predicts** stress fields using Physics-Informed Neural Networks (PINN)
- **Forecasts** damage evolution using LSTM models
- **Visualizes** crack patterns and structural health in 3D using Unity3D
- **Detects** cracks from images using UNet deep learning segmentation

---

## 📊 SYSTEM ARCHITECTURE

```
┌─────────────────────────────────────────────────────────────────┐
│                     UNITY3D (Frontend)                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  DigitalTwinVisualizer   ← Renders 3D beam with cracks        │
│  ↓                                                              │
│  DigitalTwinController   ← Coordinates AI models               │
│  ↓                                                              │
│  UNetClient (WebSocket)  ← Communicates with Python            │
│  ↓                                                              │
│  PINNRunner (Barracuda)  ← Runs PINN model locally             │
│  LSTMRunner (Barracuda)  ← Runs LSTM model locally             │
│  ScalerManager           ← Normalizes/denormalizes values      │
│                                                                 │
└────────────────────────────┬──────────────────────────────────┘
                             │ WebSocket (ws://localhost:8000)
                             │
┌────────────────────────────┴──────────────────────────────────┐
│                  PYTHON FastAPI (Backend)                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Option 1: server.py (Physics Simulation + PINN/LSTM)         │
│  ├─ Loads: PINN, LSTM, Scaler Parameters                       │
│  ├─ Simulates: Damage evolution, stress field                  │
│  ├─ Predicts: RUL (Remaining Useful Life)                      │
│                                                                 │
│  Option 2: newServer.py (UNet Crack Detection)                │
│  ├─ Loads: UNet model (ResNet34 encoder)                       │
│  ├─ Accepts: Base64-encoded images                             │
│  ├─ Returns: Crack segmentation masks                          │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

---

## 🔄 DATA FLOW - OPTION 1: Physics-Based Simulation (server.py)

### Step 1: Connection
```
Start → Unity WebSocket connects to ws://localhost:8000/ws
```

### Step 2: Server Loop (Every 0.5 seconds)
The Python server continuously:

#### A) **Crack Severity Assessment**
```python
crack_severity = damage_state  # Evolves over time (0 to 1)
```

#### B) **PINN Stress Prediction**
For each grid point on the beam:
```
Input to PINN:
  - x, y coordinate (physical position)
  - load_mag (applied load)
  - global_deflection
  - fc (concrete compressive strength)
  - fy (steel yield strength)
  
Output from PINN:
  - Base stress value
  - Amplified by damage: stress = base_stress * (1 + 2.5 * crack_severity)
```

This creates a **stress field** across the entire beam (50×50 grid = 2,500 points)

#### C) **Damage Evolution Law**
```python
max_stress = highest stress in the field
growth_rate = 0.002 + 0.00000004 * max_stress
damage_state = min(1.0, damage_state + growth_rate)
```

The beam gets progressively more damaged as stress increases.

#### D) **LSTM Prognostics**
```python
Input: Last 10 time steps of [max_stress, avg_stress]
Output: damage_prediction (0 to 1)

If damage < 0.9:
    RUL = (0.9 - damage_prediction) / TIME_STEP
Else:
    RUL = 0 (Structure failed!)
```

### Step 3: Send to Unity
```json
{
    "time": 45.0,
    "stress_field": [1200.5, 1250.3, ..., 850.2],  // 2,500 values
    "damage_prediction": 0.45,                      // 0-1 scale
    "rul": 112.5                                    // Time until failure (steps)
}
```

### Step 4: Unity Receives & Visualizes
```
Receive JSON → Extract stress_field
           ↓
     DigitalTwinVisualizer
           ↓
     Map stress to RGB colors
           ↓
     Color each vertex of beam mesh
           ↓
     Display on 3D beam
```


**Visualization Details:**
- **Low Stress** (no load): Green
- **High Stress**: Red (danger zone)
- **Damage Location**: Bottom-biased (tension zone in real beams)
- **Height Growth**: Cracks grow upward with increased loading

---

## 🖼️ DATA FLOW - OPTION 2: UNet Crack Detection (newServer.py)

### Step 1: Image Capture
```
Camera captures beam surface image
       ↓
Texture2D in Unity
```

### Step 2: Send to Server
```csharp
string base64 = Convert.ToBase64String(tex.EncodeToPNG());
string json = "{\"image\":\"" + base64 + "\"}";
await ws.SendText(json);
```

### Step 3: Server Processing
```python
# Decode image
image_bytes = base64.b64decode(message["image"])
img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

# Resize to 512×512
img = cv2.resize(img, (512, 512))

# Normalize & standardize
img = img.astype(np.float32) / 255.0
img = (img - mean) / std

# Run UNet
output = model(img_tensor)
probs = torch.sigmoid(output)

# Auto-scale: Enhance weak signals
mask = (probs / max_prob) * 255.0
mask = (mask > 128).astype(np.uint8) * 255
```

### Step 4: Return Mask
```json
{
    "mask": "iVBORw0KGgoAAAANSUhEUgAA..."  // Base64-encoded PNG
}
```

### Step 5: Unity Receives & Visualizes
```
Decode Base64 → Texture2D
              ↓
         Invert Colors (PyTorch format → Unity format)
              ↓
         Apply to Beam Material (_CurrentCrackTex)
              ↓
         Extract crack height → Update damage
              ↓
         Use PINN to predict load
```

---

## 🧠 AI MODELS

### 1. **PINN (Physics-Informed Neural Network)**
- **Purpose**: Predict stress at any location on the beam
- **Inputs** (6 values):
  - x, y: Spatial coordinates
  - load_mag: Applied load
  - global_deflection: Beam deflection
  - fc: Concrete strength
  - fy: Steel yield strength
- **Output**: Stress value
- **Why PINN?**: Respects physics equations (equilibrium, compatibility)

### 2. **LSTM (Long Short-Term Memory)**
- **Purpose**: Forecast damage evolution & remaining useful life
- **Inputs**: Time sequence of [max_stress, avg_stress] (last 10 steps)
- **Output**: Predicted damage level (0-1)
- **Uses**: RUL = (0.9 - damage_pred) / time_step

### 3. **UNet (Semantic Segmentation)**
- **Purpose**: Detect cracks from images
- **Encoder**: ResNet34 (pre-trained ImageNet)
- **Output**: Binary mask (1=crack, 0=background)
- **Post-processing**: Auto-scaling for weak signals

---

## 📦 KEY COMPONENTS

### **Python Backend (server.py)**
| Component | Role |
|-----------|------|
| `FastAPI` | Web server for WebSocket communication |
| `PINN` | TensorFlow/Keras model for stress prediction |
| `LSTM` | TensorFlow/Keras model for damage prognosis |
| `Scaler` | Normalizes/denormalizes ML inputs/outputs |

### **Unity Frontend**

| Script | Purpose |
|--------|---------|
| `DigitalTwinVisualizer.cs` | Renders 3D beam with color-coded damage |
| `DigitalTwinController.cs` | Orchestrates all AI models & updates visualization |
| `UNetClient.cs` | WebSocket client for server communication |
| `PINNRunner.cs` | Runs PINN model in Unity (Barracuda neural network library) |
| `LSTMRunner.cs` | Runs LSTM model in Unity (Barracuda) |
| `ScalerManager.cs` | Handles data normalization/denormalization |

---

## 🔧 NORMALIZATION (The Scaler)

The `ScalerManager` converts real-world values to ML-friendly normalized range:

```json
{
    "x": {"mean": 525.0, "scale": 301.59, "type": "standard"},
    "load_mag": {"mean": 105451.99, "scale": 18936.68, "type": "standard"},
    ...
}
```

**Normalization**:
```
normalized = (real_value - mean) / scale
```

**Denormalization**:
```
real_value = (normalized * scale) + mean
```

---

## 🌐 CONNECTION SETUP

### Start Python Server:
```bash
cd code
uvicorn server:app --host 0.0.0.0 --port 8000
```

✅ Expected output: `Uvicorn running on http://0.0.0.0:8000`

### Open in Unity:
1. Open the `unity/` folder in Unity Hub
2. Create `BeamDigitalTwin` GameObject
3. Add components:
   - MeshFilter
   - MeshRenderer
   - BoxCollider
   - DigitalTwinVisualizer
   - DigitalTwinController
4. Assign materials & models
5. Press Play

### WebSocket Connection:
```csharp
ws = new WebSocket("ws://localhost:8000/ws");
await ws.Connect();
```

---

## 🎨 VISUALIZATION PARAMETERS

In the **DigitalTwinVisualizer** Inspector:

| Parameter | Effect |
|-----------|--------|
| `maxLoadN` | Real-world maximum load (185,490 N) |
| `maxRealDeflectionMM` | Maximum downward movement (6.7 mm) |
| `visualExaggeration` | 1.0 = realistic, >1 = amplified |
| `bottomBias` | How much cracks prefer bottom (2.0 = 2× stronger) |
| `upwardGrowth` | How fast cracks grow as load increases |
| `manualCrackU` | Crack position along beam length (0-1) |
| `manualCrackV` | Crack position along height (0-1) |
| `manualCrackSeverity` | Crack intensity (0-1) |

---

## 📊 OUTPUT EXAMPLE

**Server sends every 0.5 seconds**:
```json
{
    "time": 250.5,
    "stress_field": [1150.2, 1200.5, 1050.3, ...],
    "damage_prediction": 0.52,
    "rul": 87.3
}
```

**Interpretation**:
- ✅ Beam is in zone with 52% damage level
- ⚠️ If damage reaches 90%, structure fails
- 📈 Remaining useful life: ~87 more time steps (~43 seconds)

---

## 🚀 WORKFLOW

1. **Start Backend**: Python server runs physics simulation
2. **Start Frontend**: Unity connects to server
3. **Monitor**: Server sends stress field & damage predictions
4. **Visualize**: Beam colors change from green (safe) → yellow → red (danger)
5. **Alert**: When RUL → 0, system predicts failure
6. **Detect Cracks** (Optional): Send images to newServer.py for UNet detection
7. **Plan Maintenance**: Use RUL to schedule repairs before failure

---

## 🔐 Key Files

```
code/
├── server.py              # Main physics simulation + PINN/LSTM
├── newServer.py           # UNet crack detection
├── scaler_params.json     # Normalization parameters
└── models/
    ├── pinn_model.h5      # Physics-informed neural network
    ├── lstm_model.h5      # Damage prognostics
    └── Detection/best_unet.pth  # Crack segmentation

Assets/New folder/
├── DigitalTwinVisualizer.cs   # 3D visualization
├── DigitalTwinController.cs   # AI orchestration
├── UNetClient.cs              # WebSocket client
├── PINNRunner.cs              # PINN inference
├── LSTMRunner.cs              # LSTM inference
├── ScalerManager.cs           # Data normalization
└── scaler_params_new.json     # Copy of scaler params
```

---

## 💡 KEY INSIGHTS

✅ **Why Physics-Informed?**
- PINN respects real physics (equilibrium, material properties)
- Works even with limited training data
- Predictions are physically plausible

✅ **Why Digital Twin?**
- Virtual replica of real beam
- Continuously updated with operational data
- Can predict and prevent failures

✅ **Why Two Servers?**
- `server.py`: Simulates entire beam evolution (no images needed)
- `newServer.py`: Detects cracks from camera/sensor images (for validation)

✅ **Why Barracuda?**
- Run PINN & LSTM directly in Unity without external calls
- No latency, works offline
- GPU-accelerated

---

**Last Updated**: March 7, 2026
**Status**: Ready for integration with real sensor data
