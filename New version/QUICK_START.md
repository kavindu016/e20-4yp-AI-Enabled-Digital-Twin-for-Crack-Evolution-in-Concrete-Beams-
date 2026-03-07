# Quick Start Reference & System Overview

## 📋 YOUR PROJECT IN 2 MINUTES

**What is it?**  
An AI-powered 3D visualization system that monitors and predicts structural damage in concrete beams in real-time.

**How does it work?**  
- Python server simulates the beam's behavior using AI models (PINN, LSTM)
- Unity visualizes the results with color-coded stress maps
- WebSocket connection sends data 2x per second

**Key technologies**:
```
Backend:        Python 3.10 + FastAPI + TensorFlow
Frontend:       Unity3D + C# + Barracuda neural networks
Communication:  WebSocket (local network)
AI Models:      PINN (stress), LSTM (damage forecast), UNet (crack detection)
```

---

## 🚀 START IN 30 SECONDS

### Terminal 1: Start Python Server
```bash
cd c:\Users\acer\Desktop\2YP\e20-4yp-AI-Enabled-Digital-Twin-for-Crack-Evolution-in-Concrete-Beams-
python -m venv venv
venv\Scripts\activate
cd code
pip install -r ../requirements.txt
uvicorn server:app --host 0.0.0.0 --port 8000
```

**You should see**:
```
INFO:     Started server process [1234]
INFO:     Waiting for application startup.
✅ Uvicorn running on http://0.0.0.0:8000
```

### Terminal 2: Start Unity
```
Open Unity Hub
Add project → Select unity/ folder
Click Play ▶️
```

**You should see**:
```
Console:
✅ WebSocket Connected to Server
✅ Unity connected (from server console)
Game: 3D beam visualization starting
```

---

## 🎯 WHAT YOU'RE LOOKING AT

```
3D Beam Visualization:

   Load Direction
         ↓
    ╔════════════╗
    ║ ░░░░░░░░░ ║  ← Green = No stress
    ║ ░▓▓▓▓▓▓▓░ ║  ← Red = High stress  
    ║ ░░░░░░░░░ ║  ← Darker = Cracks
    ╚════════════╝
    └─────┬─────┘
          Support

Text Overlay:
"Load: 50 kN | Damage: 23% | RUL: 156 steps"
```

**Colors mean**:
- 🟢 **Green** = Safe (low stress)
- 🟡 **Yellow** = Caution (medium stress)
- 🔴 **Red** = Danger (high stress)
- ⚫ **Black** = Cracks detected

---

## 📊 DATA PIPELINE

```
Server Loop (Every 0.5s)
    ↓
1. PINN generates 2,500 stress values
2. LSTM predicts damage progression  
3. Send stress_field, damage_pred, RUL to Unity
    ↓
    ↓ (WebSocket)
    ↓
Unity Loop (Every frame at 60 FPS)
    ↓
1. Receive JSON from server
2. Map stress values to 3D mesh
3. Color each vertex based on stress
4. Display damage %, RUL, time
    ↓
Screen (What you see)
    ↓
3D Beam with Color Gradient
```

---

## 🔧 KEY FILES & WHAT THEY DO

### Backend (Python)

| File | Purpose |
|------|---------|
| `code/server.py` | Main simulation loop (PINN + LSTM) |
| `code/newServer.py` | UNet crack detection from images |
| `code/models/pinn_model.h5` | Physics-informed neural network |
| `code/models/lstm_model.h5` | Damage progression neural network |
| `code/scaler_params.json` | Data normalization parameters |

### Frontend (Unity)

| Script | Purpose |
|--------|---------|
| `DigitalTwinVisualizer.cs` | Renders 3D beam with colors |
| `DigitalTwinController.cs` | Coordinates all AI models |
| `UNetClient.cs` | WebSocket connection to server |
| `PINNRunner.cs` | Runs PINN in Unity (Barracuda) |
| `LSTMRunner.cs` | Runs LSTM in Unity (Barracuda) |
| `ScalerManager.cs` | Normalizes data for ML |

### Assets

| Asset | Purpose |
|-------|---------|
| `Assets/Models/pinn_model.onnx` | PINN in ONNX format for Unity |
| `Assets/Models/lstm_model.onnx` | LSTM in ONNX format for Unity |
| `Assets/New folder/scaler_params_new.json` | Copy of normalization params |
| `Assets/New folder/VertexColorUnlit.shader` | Renders vertex colors on beam |

---

## 🧪 WHAT'S HAPPENING STEP BY STEP

### Every 0.5 seconds on Python server:

```
Step 1: Grid Points (50×50)
  For each of 2,500 points on the beam:
  
Step 2: PINN Input
  Position (x, y), Load, Deflection, Material properties
  
Step 3: PINN Forward Pass
  Neural network → Stress value for that point
  
Step 4: Damage Amplification  
  stress *= (1 + damage_state_factor)
  
Step 5: Stress Field Array
  stress_field = [1150, 1200, 1050, ..., 850]  // 2,500 values
  
Step 6: Damage Evolution
  max_stress = max(stress_field)
  growth = 0.002 + 0.00000004 * max_stress
  damage_state += growth  // Cracks grow
  
Step 7: LSTM Input (after 10 steps)
  history = [[max_stress₁, avg_stress₁], ..., [max_stress₁₀, avg_stress₁₀]]
  
Step 8: LSTM Forward Pass
  damage_prediction = LSTM(history)
  RUL = (0.9 - damage_prediction) / 0.5
  
Step 9: Send to Unity
  {
    "time": 250.5,
    "stress_field": [...2500 values...],
    "damage_prediction": 0.52,
    "rul": 87.3
  }
```

### Every frame (16.67ms) on Unity:

```
Step 1: Check for WebSocket message
  if (message received within last 16.67ms):
  
Step 2: Parse JSON
  stress_field = parse message["stress_field"]
  damage_pred = parse message["damage_prediction"]
  rul = parse message["rul"]
  
Step 3: Update Visualizer
  for each vertex in beam mesh:
    stress = stress_field[nearby_grid_point]
    color = StressToColor(stress)  // Green → Red
    vertex.color = color
    
Step 4: Deform Mesh
  deflection = damage_pred * max_deflection
  vertex.position.y -= deflection
  
Step 5: Update Text
  text.text = $"Load: {load} kN | Damage: {damage}% | RUL: {rul}s"
  
Step 6: Render
  Mesh renderer draws with colors + deformation
```

---

## 🔌 CONNECTION DETAILS

**Address**: `localhost:8000`  
**Protocol**: WebSocket (ws://)  
**Port**: 8000  
**Update Rate**: 0.5 seconds (2 Hz)  
**Message Size**: ~200 KB JSON per message  
**Latency**: 10-50 ms (local network)

**Test connection**:
```csharp
// In UNetClient.OnMessage callback:
Debug.Log($"✅ Message received: {msg.Length} bytes");
```

---

## 📈 UNDERSTANDING THE NUMBERS

### Damage (0-1 scale)
```
0.00  = New beam, no cracks
0.50  = Moderate damage, visible cracks
0.90  = Critical, imminent failure
1.00  = Complete failure
```

### RUL (Remaining Useful Life)
```
RUL = (0.9 - current_damage) / TIME_STEP

Example:
  current_damage = 0.52
  RUL = (0.9 - 0.52) / 0.5 = 0.76 steps
      = 0.76 * 0.5 seconds = 0.38 seconds
  ⚠️ Structure fails in 0.38 seconds!
```

### Stress Distribution
```
Typical values: 0 to 2000 MPa
0:     Green (safe)
500:   Light yellow
1000:  Orange (caution)
1500:  Dark orange (warning)
2000:  Red (danger)
2500+: Maximum red (critical)
```

---

## ⚙️ TROUBLESHOOTING

### "WebSocket connection failed"
```
❌ Python server not running
✅ Solution: cd code && uvicorn server:app --host 0.0.0.0 --port 8000
```

### "Models not found"
```
❌ Model files in wrong location
✅ Check: code/models/pinn_model.h5 exists
✅ Check: code/models/lstm_model.h5 exists
```

### "Beam is all green, no stress shown"
```
❌ Load value too low (loadVal = 0)
✅ Increase in Inspector: DigitalTwinVisualizer → loadVal
✅ Or: Modify server.py to use higher loads
```

### "FPS drops when WebSocket sends data"
```
❌ Too much data per frame
✅ Keep server update rate at 0.5s (not more frequent)
✅ Use interpolation between updates in Unity
```

---

## 🎓 HOW TO CUSTOMIZE

### Change update frequency
**Python**:
```python
await asyncio.sleep(0.5)  # Change 0.5 to 1.0 for 1 Hz, 0.25 for 4 Hz
```

### Change beam size
**Python**:
```python
BEAM_LENGTH = 1050   # Change to 2000 for longer beam
BEAM_HEIGHT = 300    # Change to 500 for taller beam
```

### Change resolution (grid points)
**Python**:
```python
RESOLUTION = 50  # Change to 100 for finer details (slower)
```

### Change damage colors
**Unity DigitalTwinVisualizer.cs**:
```csharp
noLoadColor = Color.green;      // Change to Color.blue for no-load color
// And in StressToColor():
Color low = Color.green;        // Could be Color.blue, Color.cyan, etc
Color high = Color.red;         // Could be Color.magenta, etc
```

### Change RUL calculation
**Python server.py**:
```python
FAILURE_THRESHOLD = 0.9  # Change to 0.95 for stricter
rul = (FAILURE_THRESHOLD - damage_pred) / TIME_STEP  # Customize formula
```

---

## 🎬 TYPICAL WORKFLOW

1. **Preparation** (one-time):
   - Install Python 3.10
   - Install Unity
   - Configure WebSocket connection
   - Load AI models

2. **Simulation Start**:
   - Start Python server
   - Open Unity project
   - Press Play

3. **Monitoring** (real-time):
   - Watch beam colors change
   - Observe damage progression
   - Monitor RUL countdown
   - Check stress field distribution

4. **Analysis**:
   - Export stress field data
   - Compare LSTM predictions vs actual
   - Plan maintenance before RUL = 0

5. **Validation**:
   - Send camera image to UNet
   - Compare predicted vs detected cracks
   - Adjust model parameters if needed

---

## 📚 DOCUMENTATION FILES

| File | Read this to... |
|------|-----------------|
| `ARCHITECTURE.md` | Understand the overall system design |
| `CONNECTION_GUIDE.md` | Deep dive into data communication |
| `AI_MODELS_EXPLAINED.md` | Learn how PINN, LSTM, UNet work |
| `TECHNICAL_WALKTHROUGH.md` | See exact code execution flow |
| `guide.txt` | Original setup instructions |
| `requirements.txt` | Python dependencies |

---

## 💻 SYSTEM REQUIREMENTS

**Python**:
- Python 3.10.x (NOT 3.12)
- FastAPI, TensorFlow, PyTorch, OpenCV, etc.
- See `requirements.txt`

**Unity**:
- Unity 2021 LTS or newer
- Barracuda package (for neural networks)
- NativeWebSocket (for WebSocket connection)

**Hardware**:
- CPU: Quad-core or better
- GPU: Optional (speeds up AI inference)
- RAM: 8 GB minimum, 16 GB recommended
- Network: Localhost (same machine) recommended for low latency

---

## 🎯 NEXT STEPS

1. **✅ Get it running**: Follow the 30-second start guide
2. **📖 Understand it**: Read ARCHITECTURE.md
3. **🔧 Customize it**: Modify parameters to experiment
4. **🧪 Validate it**: Send real camera images through UNet
5. **📊 Analyze it**: Export stress fields and damage predictions

---

## 💡 KEY INSIGHTS

✅ **Physics + AI = Better predictions**
- PINN respects physics laws while learning from data
- Result: Realistic, data-backed stress predictions

✅ **Real-time visualization**
- 2 Hz updates = 0.5 second latency
- Smooth visual feedback of crack evolution

✅ **Predictive maintenance**
- RUL tells you WHEN failure will happen
- Plan repairs before catastrophic failure

✅ **Computer vision validation**
- UNet detects actual cracks from images
- Compare with AI predictions for model improvement

---

## 📞 COMMON QUESTIONS

**Q: Can I run this on a different port?**  
A: Yes, change port in server startup: `uvicorn server:app --port 9000`

**Q: Can I send real images instead of simulated data?**  
A: Yes, switch to `newServer.py` which uses UNet for crack detection

**Q: How accurate are the predictions?**  
A: PINN accuracy depends on training data. LSTM can forecast ~100 time steps ahead.

**Q: Can I run multiple beams?**  
A: Yes, extend the code to loop over multiple beams or create multiple WebSocket servers on different ports.

**Q: What if network latency is high?**  
A: Interpolate between updates in Unity. Store previous frame's data and lerp to new values.

---

**Status**: ✅ Ready to run  
**Last Updated**: March 7, 2026  
**Version**: 1.0
