# AI Models Explained: PINN, LSTM, UNet

## 🧠 THE THREE AI MODELS

Your system uses three different neural networks, each for a specific purpose:

```
┌──────────────────────────────────────────────────────────────────────┐
│                        WHAT THE AI DOES                              │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  PINN (Physics-Informed Neural Network)                              │
│  ├─ Trained on: Physics equations + real beam data                  │
│  ├─ Purpose: Predict stress at ANY location                         │
│  ├─ Input: Position (x,y) + load + material properties              │
│  ├─ Output: Stress magnitude (Pa or MPa)                            │
│  ├─ When: Runs 2,500× per server update                             │
│  └─ Benefit: Works even with limited data, respects physics!        │
│                                                                      │
│  LSTM (Long Short-Term Memory)                                       │
│  ├─ Trained on: Historical time series of damage                    │
│  ├─ Purpose: Forecast damage evolution                              │
│  ├─ Input: Last 10 time steps of [max_stress, avg_stress]          │
│  ├─ Output: Predicted damage level (0-1)                            │
│  ├─ When: Every 0.5 seconds (after history is full)                 │
│  └─ Benefit: Captures damage progression patterns!                   │
│                                                                      │
│  UNet (Semantic Segmentation)                                        │
│  ├─ Trained on: Thousands of beam images with cracks               │
│  ├─ Purpose: Detect cracks from camera/sensor images                │
│  ├─ Input: RGB image (512×512 pixels)                               │
│  ├─ Output: Binary mask (1=crack, 0=background)                     │
│  ├─ When: When you send a camera image                              │
│  └─ Benefit: Computer vision rock-solid detection!                   │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 1️⃣ PINN: Physics-Informed Neural Network

### What is PINN?

A neural network trained to respect physical laws. Unlike regular ML:

**Regular Neural Network**:
```
Input → Black Box → Output
(No physics knowledge!)
```

**PINN**:
```
Input → Learn from DATA + Learn from PHYSICS EQUATIONS → Output
(Physically plausible results!)
```

### PINN Training (What Happened Before)

```python
# This was done ONCE, offline, by your team:

# 1. Start with random network
model = tf.keras.Sequential([
    Dense(128, activation='relu'),
    Dense(256, activation='relu'),
    Dense(128, activation='relu'),
    Dense(2, activation='linear')  # Output: [deflection, stress]
])

# 2. Collect real beam data:
#    - Apply load F
#    - Measure deflection δ
#    - Calculate stress from measurements
#    - Record material properties

# 3. Loss function = DATA_loss + PHYSICS_loss
#    DATA_loss: Predictions close to measured values
#    PHYSICS_loss: Satisfy equilibrium equation: F = EA*(dδ/dx)

# 4. Train iteratively until both losses are minimized
#    Result: Model knows physics AND data!

model.save("models/pinn_model.h5")
```

### PINN in Production (What Happens Now)

You load the trained model in Python:

```python
# server.py
pinn = load_model("models/pinn_model.h5", compile=False)
```

Then use it 2,500 times per server update:

```python
# For each grid point on the beam:
X = np.array([[
    norm(phys_x, "x"),                    # Position
    norm(phys_y, "y"),                    # Position
    norm(load_mag, "load_mag"),           # Applied load
    norm(deflection, "global_deflection"), # Beam deformation
    norm(fc, "fc"),                       # Concrete strength
    norm(fy, "fy")                        # Steel yield strength
]])

stress = pinn.predict(X, verbose=0)[0][1]  # Get stress output
```

**Why PINN instead of just physics equations?**:
```
✅ Physics equations: Simple but ignore material imperfections
❌ Regular ML: Learns data but ignores physics (gives unreal answers)
✓ PINN: Combines both → Realistic + Data-driven!
```

### PINN Input/Output

```
┌──────────────────────────────┐
│  PINN Inputs (6 values)      │
├──────────────────────────────┤
│ 1. x = -525 to +525 mm       │ Horizontal position on beam
│    (Normalized by ScalerManager)
│                              │
│ 2. y = -150 to +150 mm       │ Vertical position (height)
│    (Normalized)              │
│                              │
│ 3. load_mag = 0 to 185 kN    │ Applied load magnitude
│    (Normalized)              │
│                              │
│ 4. global_deflection = 0-6.7mm │ How much beam bends down
│    (Normalized)              │
│                              │
│ 5. fc = concrete strength    │ How strong the concrete is
│    (Normalized)              │
│                              │
│ 6. fy = steel yield strength │ How strong the steel is
│    (Normalized)              │
│                              │
└──────────────────────────────┘
           ↓ (Through PINN)
┌──────────────────────────────┐
│ PINN Outputs (2 values)      │
├──────────────────────────────┤
│ 1. Deflection prediction     │
│ 2. Stress prediction (MPa)   │ ← We use this!
│                              │
│ stress = base * (1 + damage) │
│          ↑           ↑        │
│      From PINN   From state   │
└──────────────────────────────┘
```

---

## 2️⃣ LSTM: Long Short-Term Memory

### What is LSTM?

A recurrent neural network (RNN) that remembers the past:

```
Regular NN:
x[t] → Network → y[t]
(Ignores history!)


LSTM:
x[t] → ┌─────────────────┐       previous
       │  Forget Gate    │◄─────── memory
       │  Update Gate    │  
       │  Output Gate    │    state
       └─────────────────┘
                │
                ↓
            y[t], memory[t+1]
(Remembers what matters!)
```

### LSTM Training (Offline)

```python
# Historical data collected over time:
history = [
    [max_stress_t1, avg_stress_t1],  # Time step 1
    [max_stress_t2, avg_stress_t2],  # Time step 2
    ...                               # (10 time steps)
    [max_stress_t10, avg_stress_t10]  # Time step 10
]
# ↓
# Sequence input (1, 10, 2)
# ↓

model = Sequential([
    LSTM(64, return_sequences=True, input_shape=(10, 2)),
    LSTM(32),
    Dense(16, activation='relu'),
    Dense(1, activation='sigmoid')  # Output: damage (0-1)
])

# Train on historical damage progression
model.fit(X_train, y_train, epochs=100)
model.save("models/lstm_model.h5")
```

### LSTM in Production (What Happens Now)

```python
# server.py
lstm = load_model("models/lstm_model.h5", compile=False)

# Keep rolling window of history
history = deque(maxlen=10)  # Last 10 time steps

# Every loop:
history.append([max_stress, avg_stress])

if len(history) == 10:  # Window is full
    seq = np.array(history).reshape(1, 10, 2)
    damage_pred = lstm.predict(seq, verbose=0)[0][0]
    
    # Calculate remaining useful life
    if damage_pred < 0.9:
        rul = (0.9 - damage_pred) / TIME_STEP
    else:
        rul = 0.0  # FAILURE!
```

### LSTM Input/Output

```
┌──────────────────────────────────────────┐
│ LSTM Input: Time Sequence (10 steps)     │
├──────────────────────────────────────────┤
│                                          │
│ Time  │ Max Stress │ Avg Stress │        │
├───────┼────────────┼────────────┤        │
│  t-9  │    1200    │    950     │        │
│  t-8  │    1210    │    955     │        │
│  t-7  │    1220    │    960     │        │
│  t-6  │    1235    │    965     │        │
│  t-5  │    1250    │    970     │        │
│  t-4  │    1270    │    980     │        │
│  t-3  │    1290    │    985     │        │
│  t-2  │    1310    │    990     │        │
│  t-1  │    1330    │    995     │        │
│  t    │    1350    │   1000     │ ← Most recent
│                                          │
│ Shape: (1, 10, 2) = (batch, time, features)
└──────────────────────────────────────────┘
           ↓ (Through LSTM layers)
┌──────────────────────────────────────────┐
│ LSTM Output: Damage Prediction (1 value) │
├──────────────────────────────────────────┤
│                                          │
│ damage_pred = 0.52 (52% damaged)         │
│                                          │
│ RUL = (0.9 - 0.52) / 0.5 time_step     │
│     = 0.38 / 0.5                        │
│     = 76 time steps remaining            │
│     = 38 seconds (at 0.5 sec intervals) │
│                                          │
└──────────────────────────────────────────┘
```

### Why LSTM?

```
Behavior 1: Linear crack growth
  Load → Stress → Damage (predictable)
  
Behavior 2: Sudden failure
  Load → Stress → 90% damage → CRACK PROPAGATION → Collapse!
         (exponential acceleration)

LSTM learns these patterns from history!
```

---

## 3️⃣ UNet: Semantic Segmentation for Crack Detection

### What is UNet?

A CNN architecture that:
- Takes an image as input
- Outputs a pixel-wise mask (same resolution)
- Each pixel labeled as "crack" or "not-crack"

```
Input Image (512×512)       Output Mask (512×512)
┌─────────────────────┐     ┌─────────────────────┐
│ ░░/\/\/\/░░░░░░░░░ │     │ ░░111110░░░░░░░░░░ │
│ ░░░░░░░░░░░░░░░░░░ │  →  │ ░░░░░░░░░░░░░░░░░░ │
│ ░░░░░░░░░░░░░░░░░░ │     │ ░░░░░░░░░░░░░░░░░░ │
└─────────────────────┘     └─────────────────────┘
  (RGB photo)               (White = crack, Black = safe)
```

### UNet Architecture

```
Encoder (Compress Image)
  Input: 512×512
    ↓ (Conv + Pool)
  256×256
    ↓ (Conv + Pool)
  128×128
    ↓ (Conv + Pool)
  64×64 (Bottleneck)
    ↓ (Conv + Upsample)
  128×128
    ↑ (Skip connection from encoder)
    ↓ (Conv + Upsample)
  256×256
    ↑ (Skip connection from encoder)
    ↓ (Conv + Upsample)
  512×512 (Output mask)
    ↓
  Output: 0 or 1 per pixel
```

**Key Feature**: Skip connections (highways) preserve fine details from early layers!

### UNet Training (Offline)

```python
# You have paired data:
# - Images of concrete beams
# - Hand-labeled crack annotations

# Build model
model = smp.Unet(
    encoder_name="resnet34",      # Pre-trained encoder
    encoder_weights="imagenet",   # Start from real images knowledge
    in_channels=3,                # RGB
    classes=1                     # Binary output (crack / not-crack)
)

# Loss function: penalize misclassified pixels
loss_fn = DiceLoss()  # Good for segmentation
optimizer = Adam(lr=0.001)

# Train
model.fit(images, masks, epochs=50)
model.save("models/Detection/best_unet.pth")
```

### UNet in Production (newServer.py)

```python
# Load model
model = smp.Unet(
    encoder_name="resnet34",
    encoder_weights=None,  # Weights loaded separately
    in_channels=3,
    classes=1
).to(device)

model.load_state_dict(torch.load("models/Detection/best_unet.pth"))
model.eval()

# Receive image from Unity
image_bytes = base64.b64decode(message["image"])
img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

# Preprocess
img = cv2.resize(img, (512, 512))
img = img.astype(np.float32) / 255.0
mean = [0.485, 0.456, 0.406]
std = [0.229, 0.224, 0.225]
img = (img - mean) / std
img = np.transpose(img, (2, 0, 1))  # HWC → CHW
img = np.expand_dims(img, axis=0)   # Add batch dimension

# Run UNet
output = model(img_tensor)
probs = torch.sigmoid(output)  # Convert to probability (0-1)

# Auto-scaling hack (if your model is weak):
probs_np = probs.squeeze().cpu().numpy()
max_prob = np.max(probs_np)
if max_prob > 0.0001:
    mask = (probs_np / max_prob) * 255.0  # Stretch signal
else:
    mask = probs_np * 255.0

# Clean up
mask = (mask > 128).astype(np.uint8) * 255

# Send back
response = {"mask": base64.b64encode(buffer).decode("utf-8")}
await websocket.send_text(json.dumps(response))
```

### UNet Input/Output

```
Input: RGB Image (512×512 pixels)
┌─────────────────────────────────────────┐
│  From camera/sensor pointing at beam    │
│                                         │
│  ░░░/\/\/\/\/░░░░░░░░░░░░░░░░░░░░░░  │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │
│                                         │
│  (JPG/PNG image data)                   │
│                                         │
└─────────────────────────────────────────┘
           ↓ (Through Encoder)
           ↓ (Bottleneck)
           ↓ (Through Decoder + Skip connections)
Output: Binary Mask (512×512 pixels)
┌─────────────────────────────────────────┐
│  Where are the cracks?                  │
│                                         │
│  ░░░111110░░░░░░░░░░░░░░░░░░░░░░░░░░  │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │  1 = Crack pixel
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │  0 = Safe pixel
│                                         │
│  (Binary: 0 or 1 per pixel)             │
│                                         │
└─────────────────────────────────────────┘
```

---

## 🔗 HOW THEY WORK TOGETHER

### Scenario: Load is applied to beam over time

```
TIME = 0s
├─ Load = 0 N
├─ PINN calculates stress everywhere
├─ Damage = 0.05 (5%)
├─ LSTM has no prediction (history not full)
└─ UNet inactive (no image sent)

TIME = 5s
├─ Load = 50 kN
├─ PINN recalculates stress (2,500 points)
├─ Max stress ↑
├─ Damage evolves: 0.05 → 0.08
├─ Beam visualized with stress colors
└─ UNet could detect cracks if image sent

TIME = 25s (history full: 50 time steps × 0.5s)
├─ Load = 185 kN (maximum)
├─ PINN → Very high stress everywhere
├─ Growth rate accelerates (1000+ growth/step)
├─ Damage: 0.45
├─ LSTM now predicts: damage_pred = 0.48
├─ RUL = (0.9 - 0.48) / TIME_STEP = 84 steps remaining
├─ Warnings: "Critical - 84 seconds until failure!"
└─ Optional: Send camera image to UNet for verification

TIME = 67s (Approaching failure)
├─ Damage: 0.87
├─ PINN sees stress > 2000 MPa everywhere
├─ LSTM predicts: 0.89
├─ RUL = (0.9 - 0.89) / 0.5 = 2 steps → ⚠️ IMMINENT FAILURE
├─ UNet detects major crack pattern (if image sent)
└─ System recommendation: STOP TEST / REPAIR STRUCTURE

TIME = 70s
├─ Damage: 0.92
├─ LSTM predicts: 0.91
├─ RUL = (0.9 - 0.91) / 0.5 = -2 (already failed!)
├─ Stress field shows stress concentration zones
└─ ❌ STRUCTURAL FAILURE
```

---

## 📊 MODEL COMPARISON

| Aspect | PINN | LSTM | UNet |
|--------|------|------|------|
| **Type** | Physics + Data | Temporal | Computer Vision |
| **Learns** | Physics laws + patterns | Time patterns | Visual patterns |
| **Input** | Position, load, material | Time sequence | Image |
| **Output** | Stress (MPa) | Damage (0-1) | Crack mask |
| **Runs** | 2,500× per update | 1× per update | On demand |
| **Latency** | <1ms per call | <1ms | ~100ms |
| **Data Needed** | Physics + real data | Long history | Many images |
| **Unique Benefit** | Respects physics | Predicts future | Visual verification |

---

## 🚀 DEPLOYMENT

### In Python Server
```python
# Load all models once at startup
pinn = load_model("models/pinn_model.h5")
lstm = load_model("models/lstm_model.h5")
unet_model = smp.Unet(...).load("models/Detection/best_unet.pth")

# Use millions of times in production
```

### In Unity (Barracuda)
```csharp
// Load ONNX versions of models
pinnRunner.modelAsset = // pinn_model.onnx
lstmRunner.modelAsset = // lstm_model.onnx

// Run with GPU acceleration if available
worker = WorkerFactory.CreateWorker(WorkerFactory.Type.Auto, model);
```

---

## 💡 KEY TAKEAWAY

✅ **PINN**: "What's the stress right now?"
✅ **LSTM**: "What will damage be in 10 steps?"
✅ **UNet**: "Show me where the cracks are!"

Together they provide:
- **Real-time monitoring** (PINN)
- **Early warning** (LSTM with RUL)
- **Visual confirmation** (UNet)
