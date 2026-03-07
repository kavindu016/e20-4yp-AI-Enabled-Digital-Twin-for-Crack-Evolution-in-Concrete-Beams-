# 📋 Complete Documentation Index

## 🎯 Where to Start?

**New to the project?** → Start with [QUICK_START.md](QUICK_START.md)  
**Want a big picture view?** → Read [ARCHITECTURE.md](ARCHITECTURE.md)  
**Need connection details?** → Check [CONNECTION_GUIDE.md](CONNECTION_GUIDE.md)  
**Curious about AI models?** → Study [AI_MODELS_EXPLAINED.md](AI_MODELS_EXPLAINED.md)  
**Need code-level walkthrough?** → Review [TECHNICAL_WALKTHROUGH.md](TECHNICAL_WALKTHROUGH.md)

---

## 📚 Documentation Files

### 🚀 [QUICK_START.md](QUICK_START.md) - **START HERE**
**Read this first!** 30-second startup guide + reference sheet
- ✅ How to run the system in 30 seconds
- ✅ Understanding the visualization
- ✅ Troubleshooting common issues
- ✅ Customization tips
- ✅ FAQ

**Time to read**: 5-10 minutes

---

### 🏗️ [ARCHITECTURE.md](ARCHITECTURE.md) - System Overview
Complete architectural breakdown of the entire system
- ✅ Project overview and purpose
- ✅ System architecture diagram
- ✅ Data flow for Option 1 (Physics Simulation)
- ✅ Data flow for Option 2 (UNet Crack Detection)
- ✅ AI models explanation
- ✅ Key components (Python + Unity)
- ✅ Normalization details (Scaler)
- ✅ Connection setup
- ✅ Visualization parameters
- ✅ Output examples
- ✅ Complete workflow

**Time to read**: 15-20 minutes

---

### 🔌 [CONNECTION_GUIDE.md](CONNECTION_GUIDE.md) - Network Communication
Deep dive into how Unity connects to Python via WebSocket
- ✅ Complete connection workflow (startup to continuous operation)
- ✅ Phase 1: Initialization
- ✅ Phase 2: Continuous communication loop visualization
- ✅ Stress field mapping to 3D mesh
- ✅ Message format details (JSON structure)
- ✅ UNet image detection workflow
- ✅ Technical details of C# ↔ Python communication
- ✅ Data normalization deep dive
- ✅ Setup checklist
- ✅ Performance metrics

**Time to read**: 15-20 minutes

---

### 🧠 [AI_MODELS_EXPLAINED.md](AI_MODELS_EXPLAINED.md) - Neural Networks
Detailed explanation of PINN, LSTM, and UNet models
- ✅ What each AI model does (overview)
- ✅ PINN: Physics-Informed Neural Network
  - What is PINN?
  - PINN training process
  - PINN in production
  - Why PINN over regular ML?
  - Input/output details
- ✅ LSTM: Long Short-Term Memory Network
  - What is LSTM?
  - LSTM training process
  - LSTM in production
  - Why LSTM for damage prediction?
  - Input/output with examples
- ✅ UNet: Semantic Segmentation
  - What is UNet?
  - UNet architecture
  - UNet training process
  - UNet in production
  - Input/output examples
- ✅ How all three models work together (scenario walkthrough)
- ✅ Model comparison table
- ✅ Deployment details
- ✅ Key takeaway

**Time to read**: 20-25 minutes

---

### 💻 [TECHNICAL_WALKTHROUGH.md](TECHNICAL_WALKTHROUGH.md) - Code Execution
Step-by-step code walkthrough of the entire process
- ✅ Part 1: Initialization
  - Python server startup
  - Unity startup
  - WebSocket connection established
- ✅ Part 2: Continuous simulation loop (every 0.5 seconds)
  - Phase 2A: Get crack severity
  - Phase 2B: Calculate stress field using PINN
  - Phase 2C: Calculate max/average stress
  - Phase 2D: LSTM prediction
  - Phase 2E: Send to Unity
- ✅ Part 3: Unity receives & processes
  - Step 1: Receive message
  - Step 2: DigitalTwinController processes
  - Step 3: ScalerManager normalizes/denormalizes
  - Step 4: PINNRunner executes in Unity
  - Step 5: DigitalTwinVisualizer updates display
  - Step 6: Frame rendered
- ✅ Sequence diagram showing entire flow
- ✅ Debugging tips
- ✅ Performance metrics

**Time to read**: 20-30 minutes

---

## 🗂️ Other Files in Repository

### Original Documentation
- `guide.txt` - Original step-by-step setup guide (Unity 3D beam creation)
- `README.md` - Template readme (needs content)
- `requirements.txt` - Python package dependencies

### Code Files
- `code/server.py` - Main Python backend (PINN + LSTM simulation)
- `code/newServer.py` - Alternative backend (UNet crack detection)
- `code/scaler_params.json` - AI model normalization parameters

### AI Models
- `code/models/pinn_model.h5` - Physics-informed neural network
- `code/models/lstm_model.h5` - Damage progression network
- `code/models/Detection/best_unet.pth` - Crack detection network

### Unity Project
- `unity/` - Complete Unity project folder
  - Assets with C# scripts, materials, shaders
  - Scenes for visualization
  - NativeWebSocket plugin

### Assets (3D Models & Data)
- `Assets/Models/lstm_model.onnx` - LSTM in ONNX format
- `Assets/Models/pinn_model.onnx` - PINN in ONNX format
- `Assets/New folder/` - All Unity scripts and materials

---

## 🎯 Read by Purpose

### "I just want to run it"
1. [QUICK_START.md](QUICK_START.md) - 30-second guide section
2. Terminal commands for starting servers
3. Press Play in Unity
✅ Should work in 2 minutes!

### "I want to understand the system"
1. [QUICK_START.md](QUICK_START.md) - Overview section
2. [ARCHITECTURE.md](ARCHITECTURE.md) - Full system design
3. [CONNECTION_GUIDE.md](CONNECTION_GUIDE.md) - How components talk
✅ Should take 45 minutes

### "I want to modify the code"
1. [TECHNICAL_WALKTHROUGH.md](TECHNICAL_WALKTHROUGH.md) - Exact code flow
2. [ARCHITECTURE.md](ARCHITECTURE.md) - Component purposes
3. Look at actual code files:
   - `code/server.py` - Python logic
   - `Assets/New folder/DigitalTwinVisualizer.cs` - Visualization
   - `Assets/New folder/UNetClient.cs` - WebSocket logic
✅ Should take 1-2 hours

### "I want to improve the ML models"
1. [AI_MODELS_EXPLAINED.md](AI_MODELS_EXPLAINED.md) - Model understanding
2. `code/server.py` - Model usage
3. Model files:
   - Understanding PINN architecture
   - Understanding LSTM training
   - Understanding UNet for segmentation
✅ Should take 2-4 hours

### "I need to debug something"
1. [QUICK_START.md](QUICK_START.md) - Troubleshooting section
2. [CONNECTION_GUIDE.md](CONNECTION_GUIDE.md) - Common connection issues
3. [TECHNICAL_WALKTHROUGH.md](TECHNICAL_WALKTHROUGH.md) - Debug prints
4. Check console logs in Python & Unity
✅ Should identify issue in 10-20 minutes

---

## 🔑 Key Concepts Defined

### PINN (Physics-Informed Neural Network)
A neural network trained to respect physical laws. Predicts stress at any location on the beam given position, load, and material properties.

### LSTM (Long Short-Term Memory)
A recurrent neural network that remembers past patterns. Predicts future damage evolution based on historical stress data.

### UNet (Semantic Segmentation)
A computer vision model that segments images pixel-by-pixel. Detects cracks by labeling each pixel as "crack" or "background".

### WebSocket
A persistent two-way network connection. Python server sends stress data to Unity 2× per second (~0.5 second updates).

### Stress Field
A 50×50 grid (2,500 points) of stress values across the entire beam surface. Generated by PINN model.

### Damage State
A single number (0-1) indicating how badly the beam is cracked. 0 = new, 1 = failed.

### RUL (Remaining Useful Life)
Estimated time (in time steps) until the beam reaches 90% damage and fails. Predicted by LSTM.

### Scaler/Normalization
Process of converting real-world values (e.g., load in Newtons) to ML-friendly range (-1 to 1). Managed by ScalerManager.

### Barracuda
Unity's neural network inference library. Allows running PINN and LSTM directly in Unity without calling Python.

---

## 🚦 Reading Recommendations by Experience Level

### Beginner
**Time: 1 hour**
1. [QUICK_START.md](QUICK_START.md) (10 min)
2. [ARCHITECTURE.md](ARCHITECTURE.md) main overview (15 min)
3. Run project and observe (15 min)
4. [CONNECTION_GUIDE.md](CONNECTION_GUIDE.md) startup section (20 min)

### Intermediate
**Time: 2 hours**
1. All of [QUICK_START.md](QUICK_START.md) (15 min)
2. All of [ARCHITECTURE.md](ARCHITECTURE.md) (20 min)
3. All of [CONNECTION_GUIDE.md](CONNECTION_GUIDE.md) (30 min)
4. [AI_MODELS_EXPLAINED.md](AI_MODELS_EXPLAINED.md) overview sections (25 min)
5. Run & experiment (30 min)

### Advanced
**Time: 4+ hours**
1. All documentation files in order:
   - [QUICK_START.md](QUICK_START.md) (for reference)
   - [ARCHITECTURE.md](ARCHITECTURE.md) (systems view)
   - [CONNECTION_GUIDE.md](CONNECTION_GUIDE.md) (network layer)
   - [AI_MODELS_EXPLAINED.md](AI_MODELS_EXPLAINED.md) (ML deep dive)
   - [TECHNICAL_WALKTHROUGH.md](TECHNICAL_WALKTHROUGH.md) (code level)
2. Read source code:
   - `code/server.py`
   - `Assets/New folder/*.cs`
3. Experiment with modifications
4. Train your own models on custom data

---

## 📊 Quick Reference

| What do you want? | Go to | Section |
|---|---|---|
| Run it | QUICK_START | "Start in 30 seconds" |
| Understand it | ARCHITECTURE | "System Architecture" |
| Debug it | QUICK_START | "Troubleshooting" |
| Modify code | TECHNICAL_WALKTHROUGH | Any part |
| Learn AI | AI_MODELS_EXPLAINED | Model of interest |
| Network details | CONNECTION_GUIDE | "PHASE 2: Continuous" |
| Performance | CONNECTION_GUIDE | "Performance Metrics" |
| Example output | ARCHITECTURE | "Output Example" |

---

## 📞 Document Legend

```
✅ - Key information (must read)
⚠️ - Important warning or caution
💡 - Useful insight or tip
🔄 - Repeating/looping process
📊 - Data visualization or diagram
🎯 - Target or goal
📦 - Code structure or file
```

---

## 🎬 Typical Reader Journey

```
Day 1 (1 hour):
├─ Read QUICK_START section: "2 minutes"
├─ Start servers
└─ Press Play, see results

Day 2 (2 hours):
├─ Read ARCHITECTURE.md
├─ Read CONNECTION_GUIDE.md (startup section)
├─ Understand data flow
└─ Modify loadVal in Inspector to see colors change

Day 3 (2 hours):
├─ Read CONNECTION_GUIDE.md (rest)
├─ Read TECHNICAL_WALKTHROUGH.md
├─ Understand exact code execution
└─ Try modifying server.py parameters

Week 2+ (self-paced):
├─ Read AI_MODELS_EXPLAINED.md deeply
├─ Train your own PINN/LSTM models
├─ Integrate real sensor data
└─ Validate with UNet crack detection
```

---

## 💾 How to Use These Documents

1. **Online**: View in GitHub / text editor
2. **Offline**: Download all `.md` files
3. **Print**: Each document is ~5-10 pages when printed
4. **Search**: Use Ctrl+F to find specific topics
5. **Cross-reference**: Links between documents (auto-generated)

---

## 🎓 Learning Outcomes

After reading this documentation, you will understand:

✅ What this project does and why  
✅ How Python backend simulates the beam  
✅ How Unity frontend visualizes the results  
✅ How these two systems communicate via WebSocket  
✅ How PINN models predict stress  
✅ How LSTM models forecast damage  
✅ How UNet detects cracks from images  
✅ How to start the system  
✅ How to debug and troubleshoot  
✅ How to customize and modify the code  

---

**Recommended starting point**: [QUICK_START.md](QUICK_START.md)  
**Typical time investment**: 1-4 hours depending on depth needed  
**Created**: March 7, 2026  
**Status**: Complete & Ready to Use ✅
