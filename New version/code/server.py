import json
import numpy as np
import asyncio
from fastapi import FastAPI, WebSocket
from tensorflow.keras.models import load_model
from collections import deque

# ================= DEBUG MODE =================
DEBUG_NO_VGG = True   # Set False when VGG is integrated

# ================= LOAD MODELS =================
# vgg = load_model("models/vgg16_crack_best.h5", compile=False)  # later
pinn = load_model("models/PINN/pinn_model.h5", compile=False)
lstm = load_model("models/lstm_model.h5", compile=False)

# ================= LOAD SCALERS =================
with open("scaler_params.json") as f:
    scalers = json.load(f)

def norm(val, key):
    return (val - scalers[key]["mean"]) / scalers[key]["scale"]

# ================= SETTINGS =================
RESOLUTION = 10  # 🔥 REDUCED: 10x10 = 100 points (was 50x50 = 2500) - Much faster!
BEAM_LENGTH = 1050
BEAM_HEIGHT = 300
FAILURE_THRESHOLD = 0.9
TIME_STEP = 1.0

# ================= STATE =================
history = deque(maxlen=10)
time = 0.0
damage_state = 0.3   # 🔴 Start at 30% for faster demo
TEST_MODE = True     # 🔥 ACCELERATE damage growth for testing

app = FastAPI()

@app.websocket("/ws")
async def websocket_endpoint(ws: WebSocket):
    global time, damage_state

    await ws.accept()
    print("✅ Unity connected")

    while True:

        print(f"[{time:.1f}s] Starting calculation...")

        # =====================================================
        # 1️⃣ CRACK SEVERITY (EVOLVING, NOT RANDOM)
        # =====================================================
        if DEBUG_NO_VGG:
            crack_severity = damage_state
        else:
            # crack_severity = vgg_predict(image)
            crack_severity = damage_state

        print(f"[{time:.1f}s] Calculating stress field...")

        # =====================================================
        # 2️⃣ PINN STRESS FIELD
        # =====================================================
        stress_field = []

        for y in range(RESOLUTION + 1):
            for x in range(RESOLUTION + 1):

                phys_x = (x / RESOLUTION - 0.5) * BEAM_LENGTH
                phys_y = (y / RESOLUTION - 0.5) * BEAM_HEIGHT

                X = np.array([[  
                    norm(phys_x, "x"),
                    norm(phys_y, "y"),
                    norm(50000, "load_mag"),
                    norm(5.5, "global_deflection"),
                    norm(25, "fc"),
                    norm(314, "fy")
                ]])

                base_stress = float(pinn.predict(X, verbose=0)[0][1])

                # 🔥 Damage amplifies stress
                stress = base_stress * (1.0 + 2.5 * crack_severity)

                stress_field.append(stress)

        print(f"[{time:.1f}s] Stress calculated, {len(stress_field)} points")

        # =====================================================
        # 3️⃣ DAMAGE EVOLUTION LAW (PHYSICS-INSPIRED)
        # =====================================================
        max_stress = float(np.max(stress_field))
        avg_stress = float(np.mean(stress_field))

        # Damage growth accelerates with stress
        if TEST_MODE:
            growth_rate = 0.05  # 🔥 FAST: 5% per step for demo
        else:
            growth_rate = 0.002 + 0.00000004 * max_stress  # Original slow growth
        
        damage_state = min(1.0, damage_state + growth_rate)

        history.append([max_stress, avg_stress])

        # =====================================================
        # 4️⃣ LSTM PROGNOSTICS + RUL
        # =====================================================
        damage_pred = damage_state
        rul = None

        if len(history) == history.maxlen:
            seq = np.array(history).reshape(1, history.maxlen, 2)
            damage_pred = float(lstm.predict(seq, verbose=0)[0][0])

            if damage_pred < FAILURE_THRESHOLD:
                rul = (FAILURE_THRESHOLD - damage_pred) / TIME_STEP
            else:
                rul = 0.0

        # =====================================================
        # 5️⃣ SEND TO UNITY
        # =====================================================
        print(f"[{time:.1f}s] Sending to Unity...")
        
        await ws.send_json({
            "time": time,
            "stress_field": stress_field,
            "damage_prediction": damage_pred,
            "rul": rul
        })

        # 🔎 Optional debug log
        print(f"✅ t={time:.1f} | Damage={damage_state:.3f} | MaxStress={max_stress:.1f} | RUL={rul}")

        time += TIME_STEP
        await asyncio.sleep(0.5)

