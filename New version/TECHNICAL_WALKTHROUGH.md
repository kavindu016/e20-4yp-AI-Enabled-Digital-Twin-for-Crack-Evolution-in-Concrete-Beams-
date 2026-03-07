# Technical Implementation: Step-by-Step Code Walkthrough

## 🔍 COMPLETE DATA FLOW WITH CODE

Let me trace exactly what happens when you press Play in Unity.

---

## PART 1: INITIALIZATION (First frame)

### Step 1: Python Server Starts

**Terminal**:
```bash
cd code
uvicorn server:app --host 0.0.0.0 --port 8000
```

**server.py - Lines 1-35**:
```python
import json, numpy as np, asyncio
from fastapi import FastAPI, WebSocket
from tensorflow.keras.models import load_model

# ✅ LOAD MODELS (happens once at startup)
pinn = load_model("models/pinn_model.h5", compile=False)  # Physics
lstm = load_model("models/lstm_model.h5", compile=False)  # Prognostics

# ✅ LOAD SCALERS (convert real-world ↔ normalized)
with open("scaler_params.json") as f:
    scalers = json.load(f)

def norm(val, key):
    """Convert real value to normalized [-1, 1] range"""
    return (val - scalers[key]["mean"]) / scalers[key]["scale"]

# ✅ SETTINGS
RESOLUTION = 50           # 50×50 grid = 2,500 points
BEAM_LENGTH = 1050        # mm
BEAM_HEIGHT = 300         # mm
FAILURE_THRESHOLD = 0.9   # Damage level at failure
TIME_STEP = 1.0

# ✅ STATE (global variables)
history = deque(maxlen=10)  # Keep last 10 time steps
time = 0.0
damage_state = 0.05       # Start at 5% damage

app = FastAPI()
print("✅ Server ready, waiting for connections...")
```

### Step 2: Unity Starts

**UNetClient.cs - Lines 9-21**:
```csharp
async void Start()
{
    // Create WebSocket pointing to Python server
    ws = new WebSocket("ws://localhost:8000/ws");
    
    // Set callback: What to do when server sends message
    ws.OnMessage += (bytes) =>
    {
        string msg = Encoding.UTF8.GetString(bytes);
        HandleMask(msg);  // Process incoming JSON
    };
    
    // Connect!
    await ws.Connect();
    
    // Console shows: ✅ WebSocket Connected to Server
}
```

### Step 3: WebSocket Connection Established

**server.py - Lines 54-60**:
```python
@app.websocket("/ws")
async def websocket_endpoint(ws: WebSocket):
    await ws.accept()  # Accept the connection from Unity
    print("✅ Unity connected")  # Server console
    
    # Now enter infinite loop, sending data
    while True:
        # [See PART 2 below]
```

---

## PART 2: CONTINUOUS SIMULATION LOOP (Every 0.5 seconds)

**server.py - Lines 61-125**:

### Phase 2A: Get Crack Severity

```python
# 1️⃣ CRACK SEVERITY
if DEBUG_NO_VGG:
    crack_severity = damage_state  # Use damage as proxy for cracks
else:
    # In future: use VGG16 to detect cracks from image
    crack_severity = damage_state
```

**Why?** Real cracks would come from a camera. For now, we simulate them.

---

### Phase 2B: Calculate Stress Field Using PINN

```python
# 2️⃣ PINN STRESS FIELD
stress_field = []

for y in range(RESOLUTION + 1):          # 0 to 50 (51 rows)
    for x in range(RESOLUTION + 1):      # 0 to 50 (51 cols)
        
        # === Convert grid index to physical coordinates ===
        # Grid point [25, 25] → physical center (0, 0)
        # Grid point [0, 0] → left-top corner (-525, 150)
        # Grid point [50, 50] → right-bottom corner (525, -150)
        
        phys_x = (x / RESOLUTION - 0.5) * BEAM_LENGTH
        # When x=25: (25/50 - 0.5) * 1050 = (0.5 - 0.5) * 1050 = 0 ✓
        # When x=0: (0/50 - 0.5) * 1050 = -0.5 * 1050 = -525 ✓
        # When x=50: (50/50 - 0.5) * 1050 = 0.5 * 1050 = 525 ✓
        
        phys_y = (y / RESOLUTION - 0.5) * BEAM_HEIGHT
        # When y=25: (25/50 - 0.5) * 300 = 0 ✓ (middle)
        # When y=0: (0/50 - 0.5) * 300 = -150 ✓ (bottom)
        # When y=50: (50/50 - 0.5) * 300 = 150 ✓ (top)
        
        # === Build PINN input ===
        X = np.array([[
            norm(phys_x, "x"),                    # Feature 1: Position X
            norm(phys_y, "y"),                    # Feature 2: Position Y
            norm(50000, "load_mag"),              # Feature 3: Load (50 kN)
            norm(5.5, "global_deflection"),       # Feature 4: Deflection (5.5 mm)
            norm(25, "fc"),                       # Feature 5: Concrete strength
            norm(314, "fy")                       # Feature 6: Steel yield strength
        ]])
        
        # === Run PINN (inference) ===
        base_stress = float(pinn.predict(X, verbose=0)[0][1])
        
        # === Apply damage amplification ===
        # Damage makes stress worse (positive feedback)
        stress = base_stress * (1.0 + 2.5 * crack_severity)
        
        stress_field.append(stress)
        
        # Loop iteration example:
        # x=0, y=0 → stress_field[0]
        # x=1, y=0 → stress_field[1]
        # ...
        # x=50, y=50 → stress_field[2500]
```

**Result**: `stress_field` is a list of 2,500 stress values

---

### Phase 2C: Calculate Max & Average Stress

```python
# 3️⃣ DAMAGE EVOLUTION LAW
max_stress = float(np.max(stress_field))      # Highest stress point
avg_stress = float(np.mean(stress_field))     # Average across entire mesh

# Damage grows faster with stress (accelerating failure)
growth_rate = 0.002 + 0.00000004 * max_stress

# Cap at 1.0 (100% damage = total failure)
damage_state = min(1.0, damage_state + growth_rate)

history.append([max_stress, avg_stress])
```

**Example**:
```
If max_stress = 1000 MPa:
  growth_rate = 0.002 + 0.00000004 * 1000
              = 0.002 + 0.00004
              = 0.00204
  damage_state = 0.05 + 0.00204 = 0.05204

If max_stress = 2000 MPa (more damage!):
  growth_rate = 0.002 + 0.00000004 * 2000
              = 0.00208
  damage_state increases faster ⚠️
```

---

### Phase 2D: LSTM Prediction (if history is ready)

```python
# 4️⃣ LSTM PROGNOSTICS + RUL
damage_pred = damage_state  # Default
rul = None

if len(history) == history.maxlen:  # history.maxlen = 10
    # Convert history to numpy array for LSTM
    seq = np.array(history).reshape(1, history.maxlen, 2)
    # Shape: (1, 10, 2) = (batch=1, time=10, features=2)
    
    # Run LSTM
    damage_pred = float(lstm.predict(seq, verbose=0)[0][0])
    
    # Calculate remaining useful life
    if damage_pred < FAILURE_THRESHOLD:  # 0.9
        # How many TIME_STEP increments until failure?
        rul = (FAILURE_THRESHOLD - damage_pred) / TIME_STEP
    else:
        rul = 0.0  # Already failed or critical
```

**Example**:
```
history = [
    [1200, 950],   # t-9
    [1210, 955],   # t-8
    ...
    [1350, 1000]   # t (current)
]

LSTM processes: "Given these 10 stress values, predict damage"
Output: damage_pred = 0.52

RUL = (0.9 - 0.52) / 1.0 = 0.38 steps
    = 0.38 * 0.5 sec = 0.19 seconds until failure!
```

---

### Phase 2E: Send to Unity

```python
# 5️⃣ SEND TO UNITY
await ws.send_json({
    "time": time,
    "stress_field": stress_field,           # [1200.5, 1050.2, ..., 750.1]
    "damage_prediction": damage_pred,       # 0.52
    "rul": rul                              # 87.3
})

print(f"t={time:.1f} | Damage={damage_state:.3f} | MaxStress={max_stress:.1f}")

# Increment time
time += TIME_STEP

# Wait before next iteration (0.5 sec = 2 Hz frequency)
await asyncio.sleep(0.5)
```

**Network packet sent**:
```json
{
    "time": 250.5,
    "stress_field": [1150.25, 1200.50, 1050.30, ..., 850.12],
    "damage_prediction": 0.52,
    "rul": 87.3
}
```

**Transmission**: WebSocket → Network → Received by Unity in ~10-50 ms

---

## PART 3: UNITY RECEIVES & PROCESSES

### Step 1: Receive Message

**UNetClient.cs - Lines 35-45**:
```csharp
void Update()
{
    // Critical! Must dispatch messages from WebSocket
    if (ws != null)
        ws.DispatchMessageQueue();  // Check if data arrived
}
```

When data arrives:

**UNetClient.cs - OnMessage callback**:
```csharp
ws.OnMessage += (bytes) =>
{
    string msg = Encoding.UTF8.GetString(bytes);
    HandleMask(msg);  // Process JSON
};

void HandleMask(string json)
{
    // For PINN mode (not UNet detection):
    // json = {"time": 250.5, "stress_field": [...], "damage_prediction": 0.52, "rul": 87.3}
    
    // Parse JSON
    DataMessage data = JsonUtility.FromJson<DataMessage>(json);
    
    // Pass to controller
    if (controller != null)
    {
        controller.ProcessStressField(data.stress_field, data.damage_prediction);
    }
}
```

### Step 2: DigitalTwinController Processes

**DigitalTwinController.cs**:
```csharp
public void ProcessMask(Texture2D mask)
{
    // Extract crack information
    float crackHeight = ExtractCrackHeight(mask);  // 0.0 to 1.0
    float currentDamage = Mathf.Clamp01(crackHeight);
    
    // Estimate deflection from damage
    float estimatedDeflectionMM = currentDamage * 6.7f;
    
    // === Build PINN input (6 values) ===
    float[] pinnInputs = new float[]
    {
        scaler.Transform(525f, scaler.parameters.x),
        scaler.Transform(0f, scaler.parameters.y),
        scaler.Transform(visualizer.loadVal, scaler.parameters.load_mag),
        scaler.Transform(estimatedDeflectionMM, scaler.parameters.global_deflection),
        scaler.Transform(-24.89f, scaler.parameters.fc),
        scaler.Transform(314.6f, scaler.parameters.fy)
    };
    
    // === Run PINN in Unity (Barracuda) ===
    float predictedOutputScaled = pinnRunner.Run(pinnInputs);
    
    // === Denormalize PINN output ===
    float realLoadN = scaler.InverseTransform(predictedOutputScaled, 
                                               scaler.parameters.load_mag);
    
    // === Update visualizer ===
    if (visualizer != null)
    {
        visualizer.loadVal = realLoadN;
        visualizer.SetExternalDamage(currentDamage);
    }
}
```

### Step 3: ScalerManager Normalizes/Denormalizes

**ScalerManager.cs**:
```csharp
public float Transform(float value, ScalerFeature feature)
{
    // value = 185490 (real load in Newtons)
    // feature.mean = 105451.99
    // feature.scale = 18936.68
    
    return (value - feature.mean) / feature.scale;
    // = (185490 - 105451.99) / 18936.68
    // = 80038.01 / 18936.68
    // = 4.23 (normalized)
}

public float InverseTransform(float scaledValue, ScalerFeature feature)
{
    // scaledValue = 4.23
    // feature.mean = 105451.99
    // feature.scale = 18936.68
    
    return (scaledValue * feature.scale) + feature.mean;
    // = (4.23 * 18936.68) + 105451.99
    // = 80040.17 + 105451.99
    // = 185492.16 (back to real load)
}
```

### Step 4: PINNRunner Executes in Unity

**PINNRunner.cs**:
```csharp
public float Run(float[] inputs)
{
    // inputs = [normalized_x, normalized_y, normalized_load, ...]
    
    // 1. Create tensor from array
    Tensor inputTensor = new Tensor(1, inputs.Length);  // (1, 6)
    for (int i = 0; i < inputs.Length; i++)
    {
        inputTensor[i] = inputs[i];
    }
    
    // 2. Execute neural network
    worker.Execute(inputTensor);
    
    // 3. Get output (peek = don't delete internal memory)
    Tensor output = worker.PeekOutput();
    float result = output[0];
    
    // 4. Clean up
    inputTensor.Dispose();  // Only dispose what we created
    
    return result;
}
```

**Flow**:
```
float[] {norm_x, norm_y, ...}
    ↓
Create Tensor (1×6 matrix)
    ↓
Neural network inference
    ↓
Output tensor
    ↓
Extract first element
    ↓
Dispose input tensor
    ↓
Return float result
```

### Step 5: DigitalTwinVisualizer Updates Display

**DigitalTwinVisualizer.cs - Update()**:
```csharp
void Update()
{
    if (mesh == null) return;
    ApplyDeformationAndColor();
    UpdateBeamText();
}

void ApplyDeformationAndColor()
{
    // Get current load from inspector/controller
    float loadRatio = Mathf.Clamp01(loadVal / maxLoadN);
    // Example: 50000 N / 185490 N = 0.27 (27% of max load)
    
    // Calculate real deflection
    float realDeflectionMM = loadRatio * maxRealDeflectionMM;
    // = 0.27 * 6.7 = 1.81 mm downward displacement
    
    // For each vertex in mesh
    for (int i = 0; i < vertices.Length; i++)
    {
        // Get stress at this location
        float stress = GetStressAtVertex(vertices[i]);
        
        // Convert stress to color
        Color stressColor = StressToColor(stress);
        
        // Bottom of beam (tension zone) gets darker color
        if (vertices[i].y < 0)
        {
            stressColor = Color.Lerp(stressColor, Color.red, bottomBias);
        }
        
        colors[i] = stressColor;
    }
    
    // Apply to mesh
    mesh.colors = colors;
    mesh.RecalculateNormals();
}

Color StressToColor(float stress)
{
    // Stress: 0 → 2000 MPa maps to Green → Red
    
    float normalized = stress / 2000f;  // 0 to 1
    
    if (normalized < 0.5f)
    {
        // Green to Yellow
        return Color.Lerp(Color.green, Color.yellow, normalized * 2f);
    }
    else
    {
        // Yellow to Red
        return Color.Lerp(Color.yellow, Color.red, (normalized - 0.5f) * 2f);
    }
}
```

### Step 6: Frame Rendered

```
┌──────────────────────────────────┐
│  Rendered 3D Scene               │
├──────────────────────────────────┤
│                                  │
│  ░░░▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░░░   │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │  ← Stressed area (red)
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │  ← Safe area (green)
│                                  │
│  Damage: 52.3%                   │
│  RUL: 87 steps (43 seconds)      │
│                                  │
└──────────────────────────────────┘
```

---

## SEQUENCE DIAGRAM

```
Python Server              Unity3D                    User's Screen
    │                         │                            │
    ├─ Load models            │                            │
    ├─ Normalize params       │                            │
    │                         │                            │
    │                   UNetClient.Start()                 │
    │                   WebSocket.Connect() ────────┐      │
    │ ◄────────────────────────────────────────────┘      │
    │ ✅ Accept connection                                │
    │                                                      │
    ├─ Loop iteration 1:                                  │
    │   1. Generate stress field (50×50 grid)             │
    │   2. Calculate max stress                           │
    │   3. Update damage_state                            │
    │   4. LSTM prediction (after 10 steps)               │
    │   Send JSON ──────────────────────┐                 │
    │                                   ↓                 │
    │                           HandleMask() receives     │
    │                           Update stress colors      │
    │                           Render frame ────────────→│ Green beam
    │                                                      │
    ├─ Loop iteration 2: (0.5s later)                     │
    │   (repeat above)                                     │
    │   Send JSON ──────────────────────┐                 │
    │                                   ↓                 │
    │                           Receive & update          │
    │                           Render frame ────────────→│ Yellow stress
    │                                                      │ areas appear
    │                                                      │
    └─ ... (continues infinitely)                         │
                                                           │
                                                When RUL→0:
                                                ✅ Warning:
                                                "Structure
                                                 will fail
                                                 in 5s!"
```

---

## DEBUGGING: Print What's Happening

### Python Side

```python
# In server.py loop:
print(f"📊 PINN generated {len(stress_field)} values")
print(f"📊 Max stress: {max_stress:.2f} MPa")
print(f"📊 Damage state: {damage_state:.3f}")
print(f"📊 Growth rate: {growth_rate:.6f}")
if rul is not None:
    print(f"⏰ RUL: {rul:.1f} steps")
print(f"📤 Sending to Unity...")
```

### Csharp Side

```csharp
Debug.Log($"✅ Received {stress_field.Length} stress values");
Debug.Log($"📊 Max stress: {stress_field.Max():F2}");
Debug.Log($"📊 Damage prediction: {damage_pred:F3}");
Debug.Log($"⏰ RUL: {rul:F1} steps");
```

---

## PERFORMANCE METRICS

```
Python Server (Per loop iteration, 0.5s):
├─ PINN forward passes: 2,500 (one per grid point)
│  └─ Time: ~200-500ms (CPU), ~50-100ms (GPU)
├─ LSTM forward pass: 1 (per 10 steps)
│  └─ Time: <10ms
└─ Network transmission: ~50ms per message

Total server time: ~300-600ms (fits in 500ms interval) ✓

Unity (Per frame, 60 FPS):
├─ Receive WebSocket message: <1ms
├─ Process JSON: <5ms
├─ Update mesh colors: ~5-10ms
├─ Render: ~10-20ms
└─ Total: ~20-40ms << 16.67ms per frame budget

⚠️ If network packet arrives at 60 FPS:
   - Only use every 4th frame (15 FPS updates)
   - Interpolate in between frames for smooth animation
```

---

**This is your complete technical flow. Every pixel you see comes from this process!**
